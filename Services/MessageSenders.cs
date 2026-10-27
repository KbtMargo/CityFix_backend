using System.Net.Http.Headers;
using System.Text;
using CityFix_backend.Services.Interfaces;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CityFix_backend.Services
{
    /// <summary>
    /// Keeps one SMTP connection open between letters (a notification batch would otherwise reconnect and
    /// re-authenticate for every message) and reconnects once if the server has dropped it.
    /// </summary>
    public sealed class SmtpEmailSender : IEmailSender, IDisposable
    {
        private readonly EmailOptions _options;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private SmtpClient? _client;

        public SmtpEmailSender(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(string to, EmailMessage email, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = email.Subject;
            message.Body = new BodyBuilder { TextBody = email.Text, HtmlBody = email.Html }.ToMessageBody();

            await _gate.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    await (await ConnectedAsync(cancellationToken)).SendAsync(message, cancellationToken);
                }
                catch (Exception ex) when (ex is ServiceNotConnectedException or IOException or SmtpProtocolException)
                {
                    Reset();
                    await (await ConnectedAsync(cancellationToken)).SendAsync(message, cancellationToken);
                }
            }
            catch
            {
                Reset();
                throw;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<SmtpClient> ConnectedAsync(CancellationToken cancellationToken)
        {
            if (_client is { IsConnected: true }) return _client;

            Reset();
            var client = new SmtpClient();
            try
            {
                // Without TLS the SMTP password would cross the network in clear text; plain connections need an explicit opt-in.
                var security = _options.SmtpUseSsl ? SecureSocketOptions.SslOnConnect
                    : _options.SmtpAllowPlaintext ? SecureSocketOptions.StartTlsWhenAvailable
                    : SecureSocketOptions.StartTls;
                await client.ConnectAsync(_options.SmtpHost!, _options.SmtpPort, security, cancellationToken);
                if (!string.IsNullOrEmpty(_options.SmtpUserName))
                {
                    await client.AuthenticateAsync(_options.SmtpUserName, _options.SmtpPassword ?? "", cancellationToken);
                }
            }
            catch
            {
                client.Dispose();
                throw;
            }
            return _client = client;
        }

        private void Reset()
        {
            _client?.Dispose();
            _client = null;
        }

        public void Dispose()
        {
            if (_client is { IsConnected: true })
            {
                try { _client.Disconnect(true); } catch (Exception) { }
            }
            Reset();
            _gate.Dispose();
        }
    }

    public class LogEmailSender : IEmailSender
    {
        private readonly ILogger<LogEmailSender> _logger;

        public LogEmailSender(ILogger<LogEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string to, EmailMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Email (not sent, Email:SmtpHost is empty) to {To}: {Subject}\n{Body}", to, message.Subject, message.Text);
            return Task.CompletedTask;
        }
    }

    public class TwilioSmsSender : ISmsSender
    {
        private readonly HttpClient _http;
        private readonly SmsOptions _options;

        public TwilioSmsSender(HttpClient http, IOptions<SmsOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task SendAsync(string phone, string text, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(_options.TwilioAccountSid ?? "")}/Messages.json");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.TwilioAccountSid}:{_options.TwilioAuthToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            var from = _options.TwilioFrom ?? "";
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = phone,
                [from.StartsWith("MG", StringComparison.Ordinal) ? "MessagingServiceSid" : "From"] = from,
                ["Body"] = text
            });

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Twilio refused the SMS ({(int)response.StatusCode}): {error}");
            }
        }
    }

    public class LogSmsSender : ISmsSender
    {
        private readonly ILogger<LogSmsSender> _logger;
        private readonly bool _development;

        public LogSmsSender(ILogger<LogSmsSender> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _development = environment.IsDevelopment();
        }

        public Task SendAsync(string phone, string text, CancellationToken cancellationToken = default)
        {
            if (_development)
            {
                _logger.LogWarning("SMS (not sent, Sms:Provider is Log) to {Phone}: {Text}", phone, text);
            }
            else
            {
                // Production logs often leave the server; keep phone numbers and message text out of them.
                _logger.LogWarning("SMS (not sent, Sms:Provider is Log) to …{PhoneTail}.", phone.Length > 4 ? phone[^4..] : "");
            }
            return Task.CompletedTask;
        }
    }
}
