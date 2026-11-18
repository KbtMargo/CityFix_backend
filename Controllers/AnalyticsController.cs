using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Services.Reports;
using CityFix_backend.ViewModels;

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
            var actor = await GetActorAsync();
            return View(new AnalyticsViewModel
            {
                Data = await _analytics.GetAnalyticsAsync(actor),
                Management = actor.IsStaff ? await _analytics.GetManagementAsync(actor) : null
            });
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
