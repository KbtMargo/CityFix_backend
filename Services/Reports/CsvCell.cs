using System.Globalization;

namespace CityFix_backend.Services.Reports
{
    public static class CsvCell
    {
        public static string Format(object? value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            if (value is string && text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                text = "'" + text;
            }
            return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }
    }
}
