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
        private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RetryBase = TimeSpan.FromSeconds(30);

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

                if (DateTime.UtcNow - _lastCleanup > CleanupInterval)
                {
                    _lastCleanup = DateTime.UtcNow;
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var removed = await scope.ServiceProvider.GetRequiredService<INotificationService>()
                            .DeleteOldAsync(_options.KeepRead, _options.KeepAny);
                        if (removed > 0) _logger.LogInformation("Removed {Count} old notifications.", removed);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Removing old notifications failed.");
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
        private DateTime _lastCleanup = DateTime.MinValue;

        private async Task DispatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var sms = scope.ServiceProvider.GetRequiredService<ISmsSender>();

            // Lease the batch: pushing NextAttemptAt forward under FOR UPDATE SKIP LOCKED means a second app instance
            // (or a run that crashed mid-batch) cannot pick the same rows and send duplicates until the lease runs out.
            var now = DateTime.UtcNow;
            var leaseUntil = now + Lease;
            var ids = await db.Database.SqlQuery<int>($"""
                UPDATE "Notifications" SET "NextAttemptAt" = {leaseUntil}
                WHERE "Id" IN (
                    SELECT "Id" FROM "Notifications"
                    WHERE ("EmailState" = {(int)DeliveryState.Pending} OR "SmsState" = {(int)DeliveryState.Pending}) AND "NextAttemptAt" <= {now}
                    ORDER BY "Id"
                    LIMIT {_options.BatchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING "Id" AS "Value"
                """).ToListAsync(cancellationToken);
            if (ids.Count == 0) return;

            var batch = await db.Notifications
                .Include(n => n.User)
                .Include(n => n.Appeal)
                .Where(n => ids.Contains(n.Id))
                .OrderBy(n => n.Id)
                .ToListAsync(cancellationToken);

            foreach (var notification in batch)
            {
                var user = notification.User!;
                var failed = false;

                if (user.IsBlocked)
                {
                    if (notification.EmailState == DeliveryState.Pending) notification.EmailState = DeliveryState.None;
                    if (notification.SmsState == DeliveryState.Pending) notification.SmsState = DeliveryState.None;
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var previous = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentUICulture = new CultureInfo(user.Language == "en" ? "en" : "uk");
                try
                {
                    var title = NotificationText.Title(notification);

                    if (notification.EmailState == DeliveryState.Pending)
                    {
                        if (!user.NotifyEmail || !user.EmailConfirmed)
                        {
                            notification.EmailState = DeliveryState.None;
                        }
                        else if (await TryAsync(() => email.SendAsync(user.Email, BuildEmail(notification, title), cancellationToken), notification, "email"))
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
                        else if (await TryAsync(() => sms.SendAsync(phone, NotificationText.Sms(notification, AppealLink(notification)), cancellationToken), notification, "SMS"))
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
                else if (failed)
                {
                    // 30 s, 1 min, 2 min, 4 min … so a provider outage is not hammered every few seconds.
                    notification.NextAttemptAt = DateTime.UtcNow + RetryBase * Math.Pow(2, notification.Attempts - 1);
                }

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private string? AppealLink(Notification notification) =>
            !string.IsNullOrWhiteSpace(_options.PublicUrl) && notification.AppealId != null
                ? $"{_options.PublicUrl.TrimEnd('/')}/Appeals?appealId={notification.AppealId}"
                : null;

        private EmailMessage BuildEmail(Notification notification, string title)
        {
            var link = AppealLink(notification);
            var lead = NotificationText.Lead(notification);
            var comment = NotificationText.Comment(notification);
            return new EmailTemplate
            {
                Subject = $"{SharedText.Get("App.Name")}: {title}",
                Preheader = lead,
                Heading = title,
                Paragraphs = { EmailTemplate.Greeting(notification.User!.FirstName), lead },
                QuoteLabel = comment == null ? null : SharedText.Get("Notifications.Comment"),
                Quote = comment,
                ButtonText = link == null ? null : SharedText.Get("Email.OpenAppeal"),
                ButtonUrl = link,
                Notes = { SharedText.Get("Email.NotificationFooter") }
            }.Render();
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
