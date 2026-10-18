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

    // "Seed": the first administrator. Without a password one is generated and logged once.
    public class SeedOptions
    {
        public const string Section = "Seed";

        public string AdminEmail { get; set; } = "admin@cityfix.local";
        public string? AdminPassword { get; set; }
    }
}
