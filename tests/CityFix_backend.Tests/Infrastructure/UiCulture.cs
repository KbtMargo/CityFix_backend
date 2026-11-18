using System.Globalization;

namespace CityFix_backend.Tests.Infrastructure;

/// <summary>Switches the UI culture for one test; xUnit runs each test on its own async flow.</summary>
public sealed class UiCulture : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

    public UiCulture(string name) => CultureInfo.CurrentUICulture = new CultureInfo(name);

    public void Dispose() => CultureInfo.CurrentUICulture = _previous;
}
