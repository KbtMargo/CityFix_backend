namespace CityFix_backend.Services
{
    // Settings from appsettings.json. Views read the limits from their view models, never from a service class.

    // "Uploads": photo limits for an appeal.
    public class UploadOptions
    {
        public const string Section = "Uploads";

        public int MaxPhotos { get; set; } = 5;
        public long MaxBytes { get; set; } = 10 * 1024 * 1024;
    }

    // "Appeals": the similar-appeals search.
    public class AppealOptions
    {
        public const string Section = "Appeals";

        public double SimilarRadiusMeters { get; set; } = 500;
        public int SimilarLimit { get; set; } = 5;
    }

    // "Geocoding": the Nominatim HTTP API. Its usage policy asks for an identifying User-Agent,
    // at most one request per second and cached results.
    public class GeocodingOptions
    {
        public const string Section = "Geocoding";

        public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
        public string UserAgent { get; set; } = "CityFix/1.0 (course project)";
        public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);

        // Searches are limited to Khmelnytskyi: west, north, east, south.
        public string ViewBox { get; set; } = "26.85,49.48,27.12,49.35";
    }

    // "Email": outgoing mail. Without Smtp:Host letters are written to the log instead (development).
    public class EmailOptions
    {
        public const string Section = "Email";

        public string FromAddress { get; set; } = "noreply@cityfix.local";
        public string FromName { get; set; } = "CityFix";

        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseSsl { get; set; }
        public string? SmtpUserName { get; set; }
        public string? SmtpPassword { get; set; }

        // How long a confirmation link stays valid.
        public TimeSpan ConfirmationLifetime { get; set; } = TimeSpan.FromDays(2);
    }

    // "Sms": "Log" writes messages to the log, "Twilio" sends them through the Twilio REST API.
    public class SmsOptions
    {
        public const string Section = "Sms";

        public string Provider { get; set; } = "Log";
        public string? TwilioAccountSid { get; set; }
        public string? TwilioAuthToken { get; set; }
        public string? TwilioFrom { get; set; }
    }

    // "Notifications": the background sender of email and SMS copies.
    public class NotificationOptions
    {
        public const string Section = "Notifications";

        public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromSeconds(15);
        public int BatchSize { get; set; } = 50;
        public int MaxAttempts { get; set; } = 5;

        // The site address for links in emails (e.g. https://cityfix.example.com). Without it emails carry no link.
        public string? PublicUrl { get; set; }
    }

    // "Translation": machine translation of appeals and of category / recipient names. DeepL when DeepLApiKey
    // is set, otherwise MyMemory. Enabled = false turns it off (everyone then sees the original text).
    public class TranslationOptions
    {
        public const string Section = "Translation";

        public bool Enabled { get; set; } = true;
        public string? DeepLApiKey { get; set; }
        public string? MyMemoryEmail { get; set; }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

        // After a failure (quota, network) the next round waits this long.
        public TimeSpan RetryAfterFailure { get; set; } = TimeSpan.FromMinutes(5);
        public int BatchSize { get; set; } = 10;

        // An appeal whose translation failed this many times is shown in its original language only.
        public int MaxAttempts { get; set; } = 5;
    }

    // "Seed": the first administrator. Without a password one is generated and logged once.
    public class SeedOptions
    {
        public const string Section = "Seed";

        public string AdminEmail { get; set; } = "admin@cityfix.local";
        public string? AdminPassword { get; set; }
    }
}
