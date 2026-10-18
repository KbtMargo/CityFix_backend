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
        private readonly IAuthService _auth;

        public AccountController(IAuthService auth, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            _auth = auth;
        }

        // GET: Account/Register
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View(new RegisterViewModel());
        }

        // POST: Account/Register  (rate-limited: invitation codes cannot be guessed by brute force)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _auth.RegisterAsync(new RegisterRequest(
                model.Email, model.Password, model.FirstName, model.LastName, model.Phone, model.InvitationCode));

            if (!result.Succeeded)
            {
                foreach (var (field, messageKey) in result.Errors)
                {
                    ModelState.AddModelError(field, L[messageKey]);
                }
                return View(model);
            }

            await SignInAsync(result.User!, persistent: false);
            FlashSuccess(L["Auth.Welcome"]);
            return RedirectToAction("Index", "Dashboard");
        }

        // GET: Account/Login
        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: Account/Login
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
                // One message for a wrong password, an unknown email and a blocked account.
                ModelState.AddModelError(string.Empty, L["Auth.LoginFailed"]);
                return View(model);
            }

            await SignInAsync(user, model.RememberMe);
            return RedirectToLocal(model.ReturnUrl, () => RedirectToAction("Index", "Dashboard"));
        }

        // POST: Account/Logout
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/AccessDenied
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        // GET: Account/Profile
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await Users.GetAsync(CurrentUserId);
            return View(new ProfileViewModel { User = user, Profile = ProfileFormViewModel.From(user) });
        }

        // POST: Account/Profile  (name, email, phone)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await Users.UpdateProfileAsync(CurrentUserId, model.ToInput());
                    FlashSuccess(L["Profile.Saved"]);
                    return RedirectToAction(nameof(Profile));
                }
                catch (ServiceValidationException ex)
                {
                    AddModelError(ex, "Profile.");
                }
            }

            var user = await Users.GetAsync(CurrentUserId);
            return View(nameof(Profile), new ProfileViewModel { User = user, Profile = model });
        }

        // POST: Account/ChangePassword
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
                var user = await Users.GetAsync(CurrentUserId);
                return View(nameof(Profile), new ProfileViewModel { User = user, Profile = ProfileFormViewModel.From(user), ChangePassword = model });
            }

            FlashSuccess(L["Profile.PasswordChanged"]);
            return RedirectToAction(nameof(Profile));
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
