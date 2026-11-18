using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<DashboardKpis> GetDashboardKpisAsync(User actor, bool community = false);
        Task<AnalyticsData> GetAnalyticsAsync(User actor);
        Task<ManagementData> GetManagementAsync(User actor);
    }
}
