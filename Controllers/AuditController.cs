using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AuditController : AppController
    {
        private readonly IAuditLog _audit;

        public AuditController(IAuditLog audit, IUserService users, IStringLocalizer<SharedResource> localizer) : base(users, localizer)
        {
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            return View(new TablePageViewModel<AuditEntry> { Page = await _audit.GetPageAsync(q, page), Q = q });
        }
    }
}
