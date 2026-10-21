using System.Globalization;
using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Services
{
    public class NotificationDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly NotificationOptions _options;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(IServiceScopeFactory scopes, IOptions<NotificationOptions> options, ILogger<NotificationDispatcher> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_options.DispatchInterval);
            do
            {
                try
                {
                    await DispatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Sending notification copies failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task DispatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var sms = scope.ServiceProvider.GetRequiredService<ISmsSender>();

            var batch = await db.Notifications
                .Include(n => n.User)
                .Include(n => n.Appeal)
                .Where(n => n.EmailState == DeliveryState.Pending || n.SmsState == DeliveryState.Pending)
                .OrderBy(n => n.Id)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var notification in batch)
            {
                var user = notification.User!;
                var failed = false;

                var previous = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentUICulture = new CultureInfo(user.Language == "en" ? "en" : "uk");
                try
                {
                    var title = NotificationText.Title(notification);
                    var text = NotificationText.Text(notification);

                    if (notification.EmailState == DeliveryState.Pending)
                    {
                        if (!user.NotifyEmail || !user.EmailConfirmed)
                        {
                            notification.EmailState = DeliveryState.None;
                        }
                        else if (await TryAsync(() => email.SendAsync(user.Email, title, EmailBody(notification, text), cancellationToken), notification, "email"))
                        {
                            notification.EmailState = DeliveryState.Sent;
                        }
                        else failed = true;
                    }

                    if (notification.SmsState == DeliveryState.Pending)
                    {
                        var phone = PhoneNumber.ToE164(user.Phone);
                        if (!user.NotifySms || phone == null)
                        {
                            notification.SmsState = DeliveryState.None;
                        }
                        else if (await TryAsync(() => sms.SendAsync(phone, $"{SharedText.Get("App.Name")}: {text}", cancellationToken), notification, "SMS"))
                        {
                            notification.SmsState = DeliveryState.Sent;
                        }
                        else failed = true;
                    }
                }
                finally
                {
                    CultureInfo.CurrentUICulture = previous;
                }

                if (failed && ++notification.Attempts >= _options.MaxAttempts)
                {
                    if (notification.EmailState == DeliveryState.Pending) notification.EmailState = DeliveryState.Failed;
                    if (notification.SmsState == DeliveryState.Pending) notification.SmsState = DeliveryState.Failed;
                }

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private string EmailBody(Notification notification, string text)
        {
            var lines = new List<string> { text };
            if (!string.IsNullOrWhiteSpace(_options.PublicUrl) && notification.AppealId != null)
            {
                lines.Add("");
                lines.Add($"{_options.PublicUrl.TrimEnd('/')}/Appeals?appealId={notification.AppealId}");
            }
            lines.Add("");
            lines.Add(SharedText.Get("Email.NotificationFooter"));
            return string.Join("\n", lines);
        }

        private async Task<bool> TryAsync(Func<Task> send, Notification notification, string channel)
        {
            try
            {
                await send();
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not send the {Channel} copy of notification {Id} (attempt {Attempt}).",
                    channel, notification.Id, notification.Attempts + 1);
                return false;
            }
        }
    }
}
