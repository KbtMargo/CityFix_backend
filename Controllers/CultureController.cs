using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CityFix_backend.Controllers
{
    public class CultureController : Controller
    {
        // POST: Culture/Set  (the UA/EN switch)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(string culture, string? returnUrl)
        {
            var uiCulture = culture == "en" ? "en" : "uk";

            // Only the UI culture changes; the formatting culture stays invariant (decimal dot for Lat/Lng).
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(CultureInfo.InvariantCulture, new CultureInfo(uiCulture))),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax
                });

            return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
    }
}
