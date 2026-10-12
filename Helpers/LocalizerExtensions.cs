using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Helpers
{
    public static class LocalizerExtensions
    {
        // Localized text for an enum value; the resource key is "<EnumType>_<Value>".
        public static string EnumText<T>(this IStringLocalizer localizer, T value) where T : struct, Enum
            => localizer[$"{typeof(T).Name}_{value}"];

        // Dropdown items for every value of an enum, with localized captions.
        public static IEnumerable<SelectListItem> EnumItems<T>(this IStringLocalizer localizer) where T : struct, Enum
            => Enum.GetValues<T>().Select(v => new SelectListItem(localizer.EnumText(v), v.ToString()));

        // Timestamps are stored in UTC; show them in the server's local time using the UI language's format.
        public static string ToUiString(this DateTime value)
            => value.ToLocalTime().ToString("g", CultureInfo.CurrentUICulture);
    }
}
