using System.Security.Claims;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Controllers
{
    // Shared plumbing for the signed-in part of the app: the current user, flash messages and
    // turning a broken business rule into a localized form error.
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

        // The signed-in user as stored now, loaded once per request.
        protected async Task<User> GetActorAsync() => _actor ??= await Users.GetActorAsync(CurrentUserId);

        // One-off feedback after a redirect (see _Flash).
        protected void FlashSuccess(string message) => TempData[SuccessKey] = message;

        protected void FlashError(string message) => TempData[ErrorKey] = message;

        protected string Message(ServiceValidationException ex) => L[ex.MessageKey, ex.Arguments];

        protected void AddModelError(ServiceValidationException ex, string prefix = "") =>
            ModelState.AddModelError(ex.Field.Length == 0 ? "" : prefix + ex.Field, Message(ex));

        // Only local return URLs are followed (no open redirects).
        protected IActionResult RedirectToLocal(string? returnUrl, Func<IActionResult> fallback) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : fallback();
    }
}
