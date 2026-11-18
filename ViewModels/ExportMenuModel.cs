namespace CityFix_backend.ViewModels
{
    public record ExportMenuModel(string Controller, IReadOnlyDictionary<string, string> Route, params string[] Formats);
}
