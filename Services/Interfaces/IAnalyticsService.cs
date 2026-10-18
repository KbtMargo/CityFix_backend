using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    // All figures are computed over the appeals the user may see.
    public interface IAnalyticsService
    {
        Task<DashboardKpis> GetDashboardKpisAsync(User actor);

        Task<AnalyticsData> GetAnalyticsAsync(User actor);
    }
}
