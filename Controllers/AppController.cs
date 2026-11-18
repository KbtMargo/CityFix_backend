using System.Security.Claims;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Controllers
{
    public abstract class AppController : Controller
    {
        public const string SuccessKey = "Success";
        public const string ErrorKey = "Error";

        protected readonly IUserService Users;
        protected readonly IStringLocalizer<SharedResource> L;

        private User? _actor;

        protected AppController(IUserService users, IStringLocalizer<SharedResource> localizer)
        {
            Users = users;
            L = localizer;
        }

        protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        protected async Task<User> GetActorAsync() => _actor ??= await Users.GetActorAsync(CurrentUserId);

        protected void FlashSuccess(string message) => TempData[SuccessKey] = message;

        protected void FlashError(string message) => TempData[ErrorKey] = message;

        protected string Message(ServiceValidationException ex) => L[ex.MessageKey, ex.Arguments];

        protected void AddModelError(ServiceValidationException ex, string prefix = "") =>
            ModelState.AddModelError(ex.Field.Length == 0 ? "" : prefix + ex.Field, Message(ex));

        protected async Task<Services.Reports.ReportHeader> ReportHeaderAsync(string title, string? filters)
        {
            var user = await Users.GetAsync(CurrentUserId);
            var scope = user.Role switch
            {
                UserRole.Administrator => L["Report.Scope.All"].Value,
                UserRole.AcabAdmin when user.Acab != null => L["Report.Scope.Acab", user.Acab.DisplayName()].Value,
                _ => L["Report.Scope.Mine"].Value
            };
            return new(title, scope, filters, user.DisplayName(), DateTime.UtcNow);
        }

        protected IActionResult RedirectToLocal(string? returnUrl, Func<IActionResult> fallback) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : fallback();
    }
}
