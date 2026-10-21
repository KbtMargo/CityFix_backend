using System.Globalization;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Controllers
{
    public class AccountController : AppController
    {
        private const string SentToKey = "ConfirmationSentTo";

        private readonly IAuthService _auth;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAuthService auth, IUserService users, IStringLocalizer<SharedResource> localizer, ILogger<AccountController> logger)
            : base(users, localizer)
        {
            _auth = auth;
            _logger = logger;
        }

        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model.HasAssociation && string.IsNullOrWhiteSpace(model.InvitationCode))
            {
                ModelState.AddModelError(nameof(model.InvitationCode), L["Auth.CodeRequired"]);
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _auth.RegisterAsync(new RegisterRequest(
                model.Email, model.Password, model.FirstName, model.LastName, model.Phone, model.HasAssociation ? model.InvitationCode : null));

            if (!result.Succeeded)
            {
                foreach (var (field, messageKey) in result.Errors)
                {
                    ModelState.AddModelError(field, L[messageKey]);
                }
                return View(model);
            }

            var user = result.User!;
            await TrySendConfirmationAsync(user, user.Email);
            TempData[SentToKey] = user.Email;
            return RedirectToAction(nameof(CheckEmail));
        }

        public IActionResult CheckEmail()
        {
            if (TempData[SentToKey] is not string email)
            {
                return RedirectToAction(nameof(Login));
            }
            return View(new ResendConfirmationViewModel { Email = email });
        }

        public async Task<IActionResult> ConfirmEmail(string? token)
        {
            var result = string.IsNullOrEmpty(token)
                ? EmailConfirmation.Invalid
                : (await _auth.ConfirmEmailAsync(token)).Result;

            switch (result)
            {
                case EmailConfirmation.Confirmed:
                    FlashSuccess(L["Auth.EmailConfirmed"]);
                    break;
                case EmailConfirmation.EmailTaken:
                    FlashError(L["Auth.EmailTaken"]);
                    break;
                default:
                    FlashError(L["Auth.ConfirmInvalid"]);
                    break;
            }

            return User.Identity?.IsAuthenticated == true
                ? RedirectToAction(nameof(Profile))
                : RedirectToAction(nameof(Login));
        }

        public IActionResult ResendConfirmation(string? email) =>
            View(new ResendConfirmationViewModel { Email = email ?? "" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _auth.FindUnconfirmedAsync(model.Email);
            if (user != null)
            {
                await TrySendConfirmationAsync(user, user.Email);
            }
            TempData[SentToKey] = EmailAddress.Normalize(model.Email);
            return RedirectToAction(nameof(CheckEmail));
        }

        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _auth.LoginAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, L["Auth.LoginFailed"]);
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, L["Auth.EmailNotConfirmed"]);
                model.NeedsConfirmation = true;
                return View(model);
            }

            await SignInAsync(user, model.RememberMe);
            await Users.SetLanguageAsync(user.Id, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            return RedirectToLocal(model.ReturnUrl, () => RedirectToAction("Index", "Dashboard"));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            return View(ProfileViewModel.For(await Users.GetAsync(CurrentUserId)));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var toConfirm = await Users.UpdateProfileAsync(CurrentUserId, model.ToInput());
                    if (toConfirm == null)
                    {
                        FlashSuccess(L["Profile.Saved"]);
                    }
                    else if (await TrySendConfirmationAsync(await Users.GetAsync(CurrentUserId), toConfirm))
                    {
                        FlashSuccess(L["Profile.EmailPending", toConfirm]);
                    }
                    return RedirectToAction(nameof(Profile));
                }
                catch (ServiceValidationException ex)
                {
                    AddModelError(ex, "Profile.");
                }
            }

            var view = ProfileViewModel.For(await Users.GetAsync(CurrentUserId));
            view.Profile = model;
            return View(nameof(Profile), view);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResendPendingEmail()
        {
            var user = await Users.GetAsync(CurrentUserId);
            if (user.PendingEmail != null && await TrySendConfirmationAsync(user, user.PendingEmail))
            {
                FlashSuccess(L["Profile.EmailPending", user.PendingEmail]);
            }
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotifications([Bind(Prefix = "Notifications")] NotificationSettingsViewModel model)
        {
            await Users.UpdateNotificationSettingsAsync(CurrentUserId, model.ToSettings());
            FlashSuccess(L["Profile.NotificationsSaved"]);
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordViewModel model)
        {
            if (ModelState.IsValid && !await _auth.ChangePasswordAsync(CurrentUserId, model.CurrentPassword, model.NewPassword))
            {
                ModelState.AddModelError("ChangePassword.CurrentPassword", L["Profile.WrongPassword"]);
            }

            if (!ModelState.IsValid)
            {
                var view = ProfileViewModel.For(await Users.GetAsync(CurrentUserId));
                view.ChangePassword = model;
                return View(nameof(Profile), view);
            }

            FlashSuccess(L["Profile.PasswordChanged"]);
            return RedirectToAction(nameof(Profile));
        }

        private async Task<bool> TrySendConfirmationAsync(User user, string email)
        {
            var link = Url.Action(nameof(ConfirmEmail), "Account", new { token = _auth.CreateEmailToken(user.Id, email) }, Request.Scheme)!;
            try
            {
                await _auth.SendConfirmationAsync(user, email, link);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send the confirmation letter to {Email}.", email);
                FlashError(L["Auth.ConfirmSendFailed"]);
                return false;
            }
        }

        private Task SignInAsync(User user, bool persistent)
        {
            return HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                _auth.CreatePrincipal(user),
                new AuthenticationProperties { IsPersistent = persistent });
        }
    }
}
