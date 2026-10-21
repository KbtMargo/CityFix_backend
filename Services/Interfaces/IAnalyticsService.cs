using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<DashboardKpis> GetDashboardKpisAsync(User actor);

        Task<AnalyticsData> GetAnalyticsAsync(User actor);
    }
}
