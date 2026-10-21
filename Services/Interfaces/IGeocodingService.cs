namespace CityFix_backend.Services.Interfaces
{
    public interface IGeocodingService
    {
        Task<string?> ReverseAsync(double lat, double lng, CancellationToken cancellationToken = default);

        Task<GeoPlace?> SearchAsync(string query, CancellationToken cancellationToken = default);
    }
}
