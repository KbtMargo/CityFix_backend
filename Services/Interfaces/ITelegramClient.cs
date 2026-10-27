namespace CityFix_backend.Services.Interfaces
{
    public record TelegramUpdate(long UpdateId, long? ChatId, string? Text);

    public interface ITelegramClient
    {
        Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default);

        /// <summary>Long-polls the Bot API for messages sent to the bot.</summary>
        Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, TimeSpan wait, CancellationToken cancellationToken);
    }

    /// <summary>The user blocked the bot or deleted the chat; messages to it will never arrive.</summary>
    public class TelegramChatGoneException : Exception
    {
        public TelegramChatGoneException(string message) : base(message) { }
    }
}
