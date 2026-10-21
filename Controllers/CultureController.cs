using System.Globalization;
using System.Security.Claims;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CityFix_backend.Controllers
{
    public class CultureController : Controller
    {
        private readonly IUserService _users;

        public CultureController(IUserService users)
        {
            _users = users;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Set(string culture, string? returnUrl)
        {
            var uiCulture = culture == "en" ? "en" : "uk";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(CultureInfo.InvariantCulture, new CultureInfo(uiCulture))),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax
                });

            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                await _users.SetLanguageAsync(userId, uiCulture);
            }

            return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
    }
}
