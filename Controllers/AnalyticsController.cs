using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Services.Reports;

namespace CityFix_backend.Controllers
{
    [Authorize]
    public class AnalyticsController : AppController
    {
        private readonly IAnalyticsService _analytics;

        public AnalyticsController(IAnalyticsService analytics, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            _analytics = analytics;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _analytics.GetAnalyticsAsync(await GetActorAsync()));
        }

        [HttpGet]
        public async Task<IActionResult> Export(string format = "xlsx")
        {
            var actor = await GetActorAsync();
            var data = await _analytics.GetAnalyticsAsync(actor);
            var kpis = await _analytics.GetDashboardKpisAsync(actor);
            var header = await ReportHeaderAsync(L["Report.Analytics.Title"], null);

            return format == "pdf"
                ? File(AnalyticsReports.Pdf(data, kpis, header), "application/pdf", ReportHeader.FileName("analytics", header.GeneratedAtUtc, "pdf"))
                : File(AnalyticsReports.Xlsx(data, kpis, header), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ReportHeader.FileName("analytics", header.GeneratedAtUtc, "xlsx"));
        }
    }
}
