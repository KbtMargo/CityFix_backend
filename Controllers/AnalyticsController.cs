using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.Controllers
{
    // Charts over the appeals the user may see (all roles; residents see their own).
    [Authorize]
    public class AnalyticsController : AppController
    {
        private readonly IAnalyticsService _analytics;

        public AnalyticsController(IAnalyticsService analytics, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            _analytics = analytics;
        }

        // GET: Analytics
        public async Task<IActionResult> Index()
        {
            return View(await _analytics.GetAnalyticsAsync(await GetActorAsync()));
        }
    }
}
