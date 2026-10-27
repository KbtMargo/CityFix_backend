using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAnalyticsService
    {
        /// <param name="community">Count every appeal (the residents' dashboard) instead of the actor's working scope.</param>
        Task<DashboardKpis> GetDashboardKpisAsync(User actor, bool community = false);

        Task<AnalyticsData> GetAnalyticsAsync(User actor);

        /// <summary>Deadlines, residents' ratings, load and hot spots for staff, within their working scope.</summary>
        Task<ManagementData> GetManagementAsync(User actor);
    }
}
