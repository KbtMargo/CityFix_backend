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
}
