namespace CityFix_backend.Services.Interfaces
{
    // Reference lists for forms and filters.
    public interface ILookupService
    {
        Task<AppealLookups> GetAsync();
    }
}
