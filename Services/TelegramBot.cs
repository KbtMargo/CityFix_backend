using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CityFix_backend.Helpers;
using CityFix_backend.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Services
{
    /// <summary>
    /// Talks to the Telegram Bot API. The token is part of every URL, so this client's HttpClient must not log requests.
    /// </summary>
    public class TelegramClient : ITelegramClient
    {
        private readonly HttpClient _http;
        private readonly TelegramOptions _options;

        public TelegramClient(HttpClient http, IOptions<TelegramOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
        {
            using var response = await _http.PostAsJsonAsync(Method("sendMessage"),
                new { chat_id = chatId, text, link_preview_options = new { is_disabled = true } }, cancellationToken);
            if (response.IsSuccessStatusCode) return;

            var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken);
            // 403: the user blocked the bot; 400 "chat not found": the chat no longer exists.
            if (response.StatusCode == HttpStatusCode.Forbidden
                || (response.StatusCode == HttpStatusCode.BadRequest && error?.Description?.Contains("chat not found", StringComparison.OrdinalIgnoreCase) == true))
            {
                throw new TelegramChatGoneException(error?.Description ?? response.StatusCode.ToString());
            }
            throw new HttpRequestException($"Telegram refused the message ({(int)response.StatusCode}): {error?.Description}");
        }

        public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, TimeSpan wait, CancellationToken cancellationToken)
        {
            var url = Method($"getUpdates?offset={offset}&timeout={(int)wait.TotalSeconds}&allowed_updates=%5B%22message%22%5D");
            var response = await _http.GetFromJsonAsync<ApiResponse<List<Update>>>(url, cancellationToken);
            return response?.Result?.Select(u => new TelegramUpdate(u.UpdateId, u.Message?.Chat?.Id, u.Message?.Text)).ToList()
                ?? new List<TelegramUpdate>();
        }

        private string Method(string method) => $"https://api.telegram.org/bot{_options.BotToken}/{method}";

        private record ApiResponse<T>(
            [property: JsonPropertyName("ok")] bool Ok,
            [property: JsonPropertyName("result")] T? Result,
            [property: JsonPropertyName("description")] string? Description);

        private record Update(
            [property: JsonPropertyName("update_id")] long UpdateId,
            [property: JsonPropertyName("message")] Message? Message);

        private record Message(
            [property: JsonPropertyName("chat")] Chat? Chat,
            [property: JsonPropertyName("text")] string? Text);

        private record Chat([property: JsonPropertyName("id")] long Id);
    }

    /// <summary>
    /// Receives messages to the bot by long polling, so no public webhook address is needed. Handles
    /// "/start CODE" from the profile link (connects the chat to the account) and "/stop" (disconnects it).
    /// Only one app instance may poll a bot at a time.
    /// </summary>
    public class TelegramBotWorker : BackgroundService
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<TelegramBotWorker> _logger;

        public TelegramBotWorker(IServiceScopeFactory scopes, ILogger<TelegramBotWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            long offset = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // A scope per round, so the typed HttpClient is not held for the life of the app.
                    using var scope = _scopes.CreateScope();
                    var telegram = scope.ServiceProvider.GetRequiredService<ITelegramClient>();
                    var users = scope.ServiceProvider.GetRequiredService<IUserService>();
                    foreach (var update in await telegram.GetUpdatesAsync(offset, Wait, stoppingToken))
                    {
                        offset = update.UpdateId + 1;
                        if (update.ChatId is long chatId && update.Text != null)
                        {
                            await HandleAsync(telegram, users, chatId, update.Text.Trim(), stoppingToken);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Polling the Telegram bot failed; retrying in {Delay}.", RetryDelay);
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }

        private async Task HandleAsync(ITelegramClient telegram, IUserService users, long chatId, string text, CancellationToken cancellationToken)
        {
            string reply;
            if (text.StartsWith("/start", StringComparison.Ordinal))
            {
                var code = text["/start".Length..].Trim();
                var user = code.Length == 0 ? null : await users.CompleteTelegramLinkAsync(code, chatId);
                reply = InLanguage(user?.Language, () => user != null
                    ? SharedText.Format("Telegram.Linked", user.FirstName)
                    : SharedText.Get(code.Length == 0 ? "Telegram.OpenFromProfile" : "Telegram.LinkInvalid"));
            }
            else if (text.StartsWith("/stop", StringComparison.Ordinal))
            {
                var language = await users.UnlinkTelegramChatAsync(chatId);
                reply = InLanguage(language, () => SharedText.Get(language != null ? "Telegram.Unlinked" : "Telegram.NotLinked"));
            }
            else
            {
                reply = InLanguage(null, () => SharedText.Get("Telegram.Help"));
            }

            try
            {
                await telegram.SendMessageAsync(chatId, reply, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not answer a Telegram chat.");
            }
        }

        private static string InLanguage(string? language, Func<string> text)
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo(language == "en" ? "en" : "uk");
            try
            {
                return text();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }
    }
}
