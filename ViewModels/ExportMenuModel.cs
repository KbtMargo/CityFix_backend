namespace CityFix_backend.ViewModels
{
    // The "Export" menu: which action to call, the route values to keep (filters), and the formats offered.
    public record ExportMenuModel(string Controller, IReadOnlyDictionary<string, string> Route, params string[] Formats);
}
