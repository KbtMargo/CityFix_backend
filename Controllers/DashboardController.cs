using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    [Authorize]
    public class DashboardController : AppController
    {
        private readonly IAppealService _appeals;
        private readonly ILookupService _lookups;
        private readonly IAnalyticsService _analytics;

        public DashboardController(IAppealService appeals, ILookupService lookups, IAnalyticsService analytics,
            IUserService users, IStringLocalizer<SharedResource> localizer) : base(users, localizer)
        {
            _appeals = appeals;
            _lookups = lookups;
            _analytics = analytics;
        }

        public async Task<IActionResult> Index([FromQuery] AppealFilter filter, int? appealId)
        {
            var actor = await GetActorAsync();
            // Residents see the whole community here (their own appeals are under "My appeals"); staff see their working scope.
            filter.Community = actor.Role == UserRole.Resident;
            return View(new DashboardViewModel
            {
                Kpis = await _analytics.GetDashboardKpisAsync(actor, filter.Community),
                Community = filter.Community,
                List = await AppealListFactory.BuildAsync(_appeals, _lookups, filter, appealId, actor, "Dashboard")
            });
        }
    }
}
