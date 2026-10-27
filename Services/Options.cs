using CityFix_backend.Models;

namespace CityFix_backend.Services
{
    public class UploadOptions
    {
        public const string Section = "Uploads";

        public int MaxPhotos { get; set; } = 5;
        public long MaxBytes { get; set; } = 10 * 1024 * 1024;

        // Relative to the content root; must stay outside wwwroot so photos are served only after an access check.
        public string Path { get; set; } = "App_Data";
    }

    public class AppealOptions
    {
        public const string Section = "Appeals";

        public double SimilarRadiusMeters { get; set; } = 500;
        public int SimilarLimit { get; set; } = 5;

        // Days to resolve an appeal, by priority.
        public int UrgentDays { get; set; } = 2;
        public int NormalDays { get; set; } = 7;
        public int LowDays { get; set; } = 14;

        // How long after "Done" the author may confirm or reopen, and how many times an appeal may be reopened.
        public int FeedbackDays { get; set; } = 14;
        public int MaxReopens { get; set; } = 3;

        public TimeSpan DueIn(AppealPriority priority) => TimeSpan.FromDays(priority switch
        {
            AppealPriority.Urgent => UrgentDays,
            AppealPriority.Low => LowDays,
            _ => NormalDays
        });
    }

    public class TelegramOptions
    {
        public const string Section = "Telegram";

        // From @BotFather. Without a token the Telegram channel is hidden.
        public string? BotToken { get; set; }
        public string? BotUsername { get; set; }

        public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(15);

        public bool Enabled => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(BotUsername);
    }

    public class SupportOptions
    {
        public const string Section = "Support";

        // Shown in the sidebar's help box; without an address the letters' sender (Email:FromAddress) is used.
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Hours { get; set; }
    }

    public class GeocodingOptions
    {
        public const string Section = "Geocoding";

        public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
        public string UserAgent { get; set; } = "CityFix/1.0 (course project)";
        public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);

        public string ViewBox { get; set; } = "26.85,49.48,27.12,49.35";
    }

    public class EmailOptions
    {
        public const string Section = "Email";

        public string FromAddress { get; set; } = "noreply@cityfix.local";
        public string FromName { get; set; } = "CityFix";

        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseSsl { get; set; }
        // Only for a local relay or a test mailbox (smtp4dev, Mailpit) that has no TLS.
        public bool SmtpAllowPlaintext { get; set; }
        public string? SmtpUserName { get; set; }
        public string? SmtpPassword { get; set; }

        public TimeSpan ConfirmationLifetime { get; set; } = TimeSpan.FromDays(2);
    }

    public class SmsOptions
    {
        public const string Section = "Sms";

        public string Provider { get; set; } = "Log";
        public string? TwilioAccountSid { get; set; }
        public string? TwilioAuthToken { get; set; }
        public string? TwilioFrom { get; set; }
    }

    public class NotificationOptions
    {
        public const string Section = "Notifications";

        public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromSeconds(15);
        public int BatchSize { get; set; } = 50;
        public int MaxAttempts { get; set; } = 5;

        // Read notifications are removed after KeepRead, all of them after KeepAny, so the table does not grow forever.
        public TimeSpan KeepRead { get; set; } = TimeSpan.FromDays(90);
        public TimeSpan KeepAny { get; set; } = TimeSpan.FromDays(365);

        public string? PublicUrl { get; set; }
    }

    public class TranslationOptions
    {
        public const string Section = "Translation";

        public bool Enabled { get; set; } = true;
        public string? DeepLApiKey { get; set; }
        public string? MyMemoryEmail { get; set; }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan RetryAfterFailure { get; set; } = TimeSpan.FromMinutes(5);
        public int BatchSize { get; set; } = 10;

        public int MaxAttempts { get; set; } = 5;
    }

    public class SeedOptions
    {
        public const string Section = "Seed";

        public string AdminEmail { get; set; } = "admin@cityfix.local";
        public string? AdminPassword { get; set; }
    }
}
