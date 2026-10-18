using CityFix_backend.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CityFix_backend.Controllers
{
    public class HomeController : Controller
    {
        // The home page is the dashboard; signed-out visitors go to the login page.
        public IActionResult Index()
        {
            return User.Identity?.IsAuthenticated == true
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction("Login", "Account");
        }

        // Re-executed for 404 and other status codes (UseStatusCodePagesWithReExecute).
        [Route("Home/Status/{code:int}")]
        public IActionResult Status(int code)
        {
            Response.StatusCode = code;
            return View(code == 404 ? "NotFound" : "Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
