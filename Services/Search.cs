namespace CityFix_backend.Services
{
    public static class Search
    {
        public static string ContainsPattern(string text) =>
            "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    }
}
