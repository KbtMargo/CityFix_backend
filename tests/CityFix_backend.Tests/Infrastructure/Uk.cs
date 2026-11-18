using CityFix_backend.Helpers;

namespace CityFix_backend.Tests.Infrastructure;

/// <summary>Ukrainian UI texts, the default language of the site, looked up from the real resources.</summary>
public static class Uk
{
    public static string Text(string key)
    {
        using var _ = new UiCulture("uk");
        return SharedText.Get(key);
    }

    public static string Format(string key, params object[] args)
    {
        using var _ = new UiCulture("uk");
        return SharedText.Format(key, args);
    }
}
