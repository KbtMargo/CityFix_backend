using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // "Home" for every role: KPI cards, search + filters, tabs and the appeals the user may see.
    [Authorize]
    public class DashboardController : AppController
    {
        private readonly IAppealService _appeals;
        private readonly IAnalyticsService _analytics;

        public DashboardController(IAppealService appeals, IAnalyticsService analytics)
        {
            _appeals = appeals;
            _analytics = analytics;
        }

        // GET: Dashboard  (?appealId= opens the detail modal)
        public async Task<IActionResult> Index([FromQuery] AppealFilter filter, int? appealId)
        {
            var user = await _appeals.GetUserAsync(CurrentUserId);
            var list = await AppealListFactory.BuildAsync(_appeals, filter, appealId, user, "Dashboard");
            if (appealId != null && list.Selected == null)
            {
                return NotFound();
            }

            return View(new DashboardViewModel
            {
                Kpis = await _analytics.GetDashboardKpisAsync(user.Id),
                List = list
            });
        }
    }
}
