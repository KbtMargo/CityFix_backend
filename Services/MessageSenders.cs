using System.Net.Http.Headers;
using System.Text;
using CityFix_backend.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CityFix_backend.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;

        public SmtpEmailSender(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(string to, string subject, string textBody, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = textBody };

            using var client = new SmtpClient();
            var security = _options.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(_options.SmtpHost!, _options.SmtpPort, security, cancellationToken);
            if (!string.IsNullOrEmpty(_options.SmtpUserName))
            {
                await client.AuthenticateAsync(_options.SmtpUserName, _options.SmtpPassword ?? "", cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    public class LogEmailSender : IEmailSender
    {
        private readonly ILogger<LogEmailSender> _logger;

        public LogEmailSender(ILogger<LogEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string to, string subject, string textBody, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Email (not sent, Email:SmtpHost is empty) to {To}: {Subject}\n{Body}", to, subject, textBody);
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
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = phone,
                ["From"] = _options.TwilioFrom ?? "",
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

        public LogSmsSender(ILogger<LogSmsSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string phone, string text, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("SMS (not sent, Sms:Provider is Log) to {Phone}: {Text}", phone, text);
            return Task.CompletedTask;
        }
    }
}
