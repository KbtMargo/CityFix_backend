using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using CityFix_backend.Models;

namespace CityFix_backend.Helpers
{
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
        public static string GetDisplayName(this Enum value)
        {
            var key = value.GetType().GetMember(value.ToString()).FirstOrDefault()
                ?.GetCustomAttribute<DisplayAttribute>()?.Name;
            return key == null ? value.ToString() : SharedText.Get(key);
        }

        public static string PillModifier(this Enum value) => value.ToString().ToLowerInvariant();

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

        public static DateTime ToKyiv(this DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Kyiv);

        public static DateTime KyivDayStartUtc(DateTime kyivDate) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(kyivDate.Date, DateTimeKind.Unspecified), Kyiv);

        public static string ToDate(this DateTime utc) => utc.ToKyiv().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        public static string ToDateTime(this DateTime utc) => utc.ToKyiv().ToString("dd.MM.yyyy, HH:mm:ss", CultureInfo.InvariantCulture);

        public static string TimeAgo(this DateTime utc)
        {
            var span = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            if (span < TimeSpan.FromMinutes(1)) return SharedText.Get("Time.JustNow");
            if (span < TimeSpan.FromHours(1)) return SharedText.Format("Time.MinutesAgo", (int)span.TotalMinutes);
            if (span < TimeSpan.FromDays(1)) return SharedText.Format("Time.HoursAgo", (int)span.TotalHours);
            if (span < TimeSpan.FromDays(7)) return SharedText.Format("Time.DaysAgo", (int)span.TotalDays);
            return utc.ToDate();
        }

        public static string Coordinate(this decimal? value) =>
            value?.ToString("0.00000", CultureInfo.InvariantCulture) ?? "";
    }
}
