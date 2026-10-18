namespace CityFix_backend.Services
{
    public static class Search
    {
        // An ILIKE "contains" pattern (escape character \) in which % and _ typed by the user are literal.
        public static string ContainsPattern(string text) =>
            "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    }
}
