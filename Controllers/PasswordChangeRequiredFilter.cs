using CityFix_backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Controllers
{
    /// <summary>
    /// Keeps a user who signed in with a password issued by someone else (seeded administrator, account created
    /// or reset by an administrator) on the profile page until they choose their own.
    /// </summary>
    public class PasswordChangeRequiredFilter : IActionFilter
    {
        private static readonly HashSet<(string Controller, string Action)> Allowed = new()
        {
            ("Account", nameof(AccountController.Profile)),
            ("Account", nameof(AccountController.ChangePassword)),
            ("Account", nameof(AccountController.Logout)),
            ("Account", nameof(AccountController.AccessDenied)),
            ("Account", nameof(AccountController.DeleteAccount)),
            ("Account", nameof(AccountController.ExportData)),
            ("Culture", nameof(CultureController.Set)),
            ("Home", nameof(HomeController.Status)),
            ("Home", nameof(HomeController.Error))
        };

        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ITempDataDictionaryFactory _tempData;

        public PasswordChangeRequiredFilter(IStringLocalizer<SharedResource> localizer, ITempDataDictionaryFactory tempData)
        {
            _localizer = localizer;
            _tempData = tempData;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.HttpContext.User.HasClaim(SecurityStamps.MustChangePasswordClaim, "true")
                || context.ActionDescriptor is not ControllerActionDescriptor action
                || Allowed.Contains((action.ControllerName, action.ActionName)))
            {
                return;
            }

            _tempData.GetTempData(context.HttpContext)[AppController.ErrorKey] = _localizer["Auth.MustChangePassword"].Value;
            context.Result = new RedirectToActionResult(nameof(AccountController.Profile), "Account", null);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
