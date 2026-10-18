using System.Security.Claims;
using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Controllers
{
    public class AccountController : AppController
    {
        private readonly IAuthService _auth;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _l;

        public AccountController(IAuthService auth, ApplicationDbContext context, IStringLocalizer<SharedResource> localizer)
        {
            _auth = auth;
            _context = context;
            _l = localizer;
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

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                    ModelState.AddModelError(field, _l[messageKey]);
                }
                return View(model);
            }

            await SignInAsync(result.User!, persistent: false);
            FlashSuccess(_l["Auth.Welcome"]);
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
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _auth.LoginAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, _l["Auth.LoginFailed"]);
                return View(model);
            }

            await SignInAsync(user, model.RememberMe);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
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
            return View();
        }

        // GET: Account/Profile
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _context.Users.Include(u => u.Acab)
                .FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null)
            {
                // The account was deleted while this cookie was still valid.
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            return View(new ProfileViewModel { User = user });
        }

        // POST: Account/ChangePassword
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordViewModel model)
        {
            if (ModelState.IsValid && !await _auth.ChangePasswordAsync(CurrentUserId, model.CurrentPassword, model.NewPassword))
            {
                ModelState.AddModelError("ChangePassword.CurrentPassword", _l["Profile.WrongPassword"]);
            }

            if (!ModelState.IsValid)
            {
                var user = await _context.Users.Include(u => u.Acab).FirstAsync(u => u.Id == CurrentUserId);
                return View(nameof(Profile), new ProfileViewModel { User = user, ChangePassword = model });
            }

            FlashSuccess(_l["Profile.PasswordChanged"]);
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
