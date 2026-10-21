namespace CityFix_backend.Services.Interfaces
{
    public interface ILookupService
    {
        Task<AppealLookups> GetAsync();
    }
}
