using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using CityFix_backend.Models;

namespace CityFix_backend.Helpers
{
    // The shared resources (Resources/SharedResource.resx = Ukrainian, .en.resx = English) for code
    // that has no IStringLocalizer at hand: enum labels, relative times, notification texts.
    public static class SharedText
    {
        private static readonly ResourceManager Resources =
            new("CityFix_backend.Resources.SharedResource", typeof(SharedResource).Assembly);

        public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public static string Format(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentUICulture, Get(key), args);
    }

    public static class EnumExtensions
    {
        // The localized [Display(Name = "...")] label of an enum value.
        public static string GetDisplayName(this Enum value)
        {
            var key = value.GetType().GetMember(value.ToString()).FirstOrDefault()
                ?.GetCustomAttribute<DisplayAttribute>()?.Name;
            return key == null ? value.ToString() : SharedText.Get(key);
        }

        // Pill modifier: the enum name lower-cased (cf-pill--inprogress, cf-pill--urgent, ...).
        public static string PillModifier(this Enum value) => value.ToString().ToLowerInvariant();

        // DataTable role tag modifier.
        public static string RoleTag(this UserRole role) => role switch
        {
            UserRole.Administrator => "admin",
            UserRole.AcabAdmin => "head",
            _ => "resident"
        };

        public static bool IsClosed(this AppealStatus status) => status is AppealStatus.Done or AppealStatus.Rejected or AppealStatus.Duplicate;
    }

    public static class UiFormat
    {
        private static readonly TimeZoneInfo Kyiv = FindKyiv();

        private static TimeZoneInfo FindKyiv()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"); }
        }

        // Stored timestamps are UTC; everything is shown in Kyiv time.
        public static DateTime ToKyiv(this DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Kyiv);

        // Start of a Kyiv calendar day, in UTC (for date filters).
        public static DateTime KyivDayStartUtc(DateTime kyivDate) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(kyivDate.Date, DateTimeKind.Unspecified), Kyiv);

        // Dates stay numeric in both languages.
        public static string ToDate(this DateTime utc) => utc.ToKyiv().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        public static string ToDateTime(this DateTime utc) => utc.ToKyiv().ToString("dd.MM.yyyy, HH:mm:ss", CultureInfo.InvariantCulture);

        // "Щойно" / "N хв тому" / "N год тому" / "N д тому", then the date.
        public static string TimeAgo(this DateTime utc)
        {
            var span = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            if (span < TimeSpan.FromMinutes(1)) return SharedText.Get("Time.JustNow");
            if (span < TimeSpan.FromHours(1)) return SharedText.Format("Time.MinutesAgo", (int)span.TotalMinutes);
            if (span < TimeSpan.FromDays(1)) return SharedText.Format("Time.HoursAgo", (int)span.TotalHours);
            if (span < TimeSpan.FromDays(7)) return SharedText.Format("Time.DaysAgo", (int)span.TotalDays);
            return utc.ToDate();
        }

        // Coordinates: 5 decimals, always with a dot.
        public static string Coordinate(this decimal? value) =>
            value?.ToString("0.00000", CultureInfo.InvariantCulture) ?? "";
    }
}
