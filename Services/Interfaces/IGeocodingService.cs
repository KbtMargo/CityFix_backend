namespace CityFix_backend.Services.Interfaces
{
    // Addresses for map points and points for addresses (Nominatim). Null when the service is unavailable.
    public interface IGeocodingService
    {
        Task<string?> ReverseAsync(double lat, double lng, CancellationToken cancellationToken = default);

        Task<GeoPlace?> SearchAsync(string query, CancellationToken cancellationToken = default);
    }
}
