using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Controllers
{
    // Shared plumbing for the signed-in part of the app.
    public abstract class AppController : Controller
    {
        public const string SuccessKey = "Success";
        public const string ErrorKey = "Error";

        protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // One-off feedback after a redirect: a cf-badge--success line or a cf-alert (see _Flash).
        protected void FlashSuccess(LocalizedString message) => TempData[SuccessKey] = message.Value;

        protected void FlashError(LocalizedString message) => TempData[ErrorKey] = message.Value;
    }
}
