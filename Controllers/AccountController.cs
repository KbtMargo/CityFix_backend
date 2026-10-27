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
using Microsoft.Extensions.Options;

namespace CityFix_backend.Controllers
{
    public class AccountController : AppController
    {
        private const string SentToKey = "ConfirmationSentTo";

        private readonly IAuthService _auth;
        private readonly ILogger<AccountController> _logger;
        private readonly string? _publicUrl;
        private readonly IAuditLog _audit;

        public AccountController(IAuthService auth, IUserService users, IStringLocalizer<SharedResource> localizer, ILogger<AccountController> logger,
            IOptions<NotificationOptions> notifications, IAuditLog audit)
            : base(users, localizer)
        {
            _auth = auth;
            _logger = logger;
            _publicUrl = notifications.Value.PublicUrl;
            _audit = audit;
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

            if (result.ExistingAccount is { } existing)
            {
                // Same page as a fresh sign-up, so the form does not reveal who has an account; the owner gets a reset link.
                await TrySendAsync(existing.Email, () => _auth.SendAccountExistsAsync(existing, ResetLink(existing)));
                TempData[SentToKey] = existing.Email;
                return RedirectToAction(nameof(CheckEmail));
            }

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

        public IActionResult ForgotPassword(string? email) =>
            View(new ResendConfirmationViewModel { Email = email ?? "" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword(ResendConfirmationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _auth.FindForPasswordResetAsync(model.Email);
            if (user != null)
            {
                await TrySendAsync(user.Email, () => _auth.SendPasswordResetAsync(user, ResetLink(user)));
            }
            else
            {
                // Only the log says so; the page answers the same either way.
                _logger.LogInformation("Password reset requested for {Email}, which has no active account.", EmailAddress.Normalize(model.Email));
            }
            // The same answer whether or not the address has an account.
            FlashSuccess(L["Auth.Forgot.Sent", EmailAddress.Normalize(model.Email)]);
            return RedirectToAction(nameof(Login));
        }

        public async Task<IActionResult> ResetPassword(string? token)
        {
            if (string.IsNullOrEmpty(token) || await _auth.FindByResetTokenAsync(token) == null)
            {
                FlashError(L["Auth.Reset.Invalid"]);
                return RedirectToAction(nameof(ForgotPassword));
            }
            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (!await _auth.ResetPasswordAsync(model.Token, model.NewPassword))
            {
                FlashError(L["Auth.Reset.Invalid"]);
                return RedirectToAction(nameof(ForgotPassword));
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            FlashSuccess(L["Auth.Reset.Done"]);
            return RedirectToAction(nameof(Login));
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

            var login = await _auth.LoginAsync(model.Email, model.Password);
            var user = login.User;
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, login.LockedOut
                    ? L["Auth.LockedOut", (int)AuthService.LockoutDuration.TotalMinutes]
                    : L["Auth.LoginFailed"]);
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
            return View(await ProfileViewAsync());
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
                    return ProfileSection("personal");
                }
                catch (ServiceValidationException ex)
                {
                    AddModelError(ex, "Profile.");
                }
            }

            var view = await ProfileViewAsync();
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
            return ProfileSection("personal");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotifications([Bind(Prefix = "Notifications")] NotificationSettingsViewModel model)
        {
            await Users.UpdateNotificationSettingsAsync(CurrentUserId, model.ToSettings());
            FlashSuccess(L["Profile.NotificationsSaved"]);
            return ProfileSection("notifications");
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordViewModel model)
        {
            User? changed = null;
            if (ModelState.IsValid && (changed = await _auth.ChangePasswordAsync(CurrentUserId, model.CurrentPassword, model.NewPassword)) == null)
            {
                ModelState.AddModelError("ChangePassword.CurrentPassword", L["Profile.WrongPassword"]);
            }

            if (!ModelState.IsValid)
            {
                var view = await ProfileViewAsync();
                view.ChangePassword = model;
                return View(nameof(Profile), view);
            }

            // The new security stamp signs out every other session; keep this one by issuing a fresh cookie.
            var current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await SignInAsync(changed!, current.Properties?.IsPersistent == true);
            FlashSuccess(L["Profile.PasswordChanged"]);
            return ProfileSection("security");
        }

        private async Task<bool> TrySendConfirmationAsync(User user, string email)
        {
            var link = PublicLink(Url.Action(nameof(ConfirmEmail), "Account", new { token = _auth.CreateEmailToken(user.Id, email) })!);
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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ExportData()
        {
            var data = await Users.ExportPersonalDataAsync(CurrentUserId);
            var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                // A download, never embedded in a page, so readable text beats HTML-safe escaping here.
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            return File(json, "application/json", $"cityfix-my-data-{DateTime.UtcNow:yyyyMMdd}.json");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> DeleteAccount(string? password)
        {
            if (string.IsNullOrEmpty(password) || !await _auth.VerifyPasswordAsync(CurrentUserId, password))
            {
                FlashError(L["Profile.Delete.WrongPassword"]);
                return ProfileSection("data");
            }

            try
            {
                await Users.AnonymizeAsync(CurrentUserId);
                // Only the id is logged: keeping the name or e-mail here would undo the deletion.
                await _audit.RecordAsync(new User { Id = CurrentUserId, FirstName = "—", Email = "deleted" },
                    "account.self-delete", $"User #{CurrentUserId}");
            }
            catch (ServiceValidationException ex)
            {
                FlashError(Message(ex));
                return RedirectToAction(nameof(Profile));
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            FlashSuccess(L["Profile.Delete.Done"]);
            return RedirectToAction(nameof(Login));
        }

        // Failures are only logged: an error shown here would reveal that the address has an account.
        private async Task TrySendAsync(string email, Func<Task> send)
        {
            try
            {
                await send();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send the password letter to {Email}.", email);
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> JoinAssociation(string? code)
        {
            var actor = await GetActorAsync();
            try
            {
                var change = await _auth.JoinAssociationAsync(actor.Id, code ?? "");
                await _audit.RecordAsync(actor, "user.association", $"User #{actor.Id}",
                    $"{change.From ?? "-"} ({change.FromRole}) -> {change.To} ({change.ToRole})");
                FlashSuccess(change.ToRole == UserRole.AcabAdmin
                    ? L["Profile.Acab.NowHead", change.To!]
                    : L["Profile.Acab.Moved", change.To!]);
            }
            catch (ServiceValidationException ex)
            {
                FlashError(Message(ex));
            }
            return ProfileSection("association");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveAssociation()
        {
            var actor = await GetActorAsync();
            try
            {
                var change = await _auth.LeaveAssociationAsync(actor.Id);
                await _audit.RecordAsync(actor, "user.association", $"User #{actor.Id}",
                    $"{change.From} ({change.FromRole}) -> {change.To ?? "-"} ({change.ToRole})");
                FlashSuccess(change.FromRole == UserRole.AcabAdmin ? L["Profile.Acab.SteppedDown"] : L["Profile.Acab.Left"]);
            }
            catch (ServiceValidationException ex)
            {
                FlashError(Message(ex));
            }
            return ProfileSection("association");
        }

        // Back to the section that was just saved rather than the top of the page.
        private RedirectResult ProfileSection(string section) => Redirect(Url.Action(nameof(Profile))! + "#" + section);

        private async Task<ProfileViewModel> ProfileViewAsync() =>
            ProfileViewModel.For(await Users.GetAsync(CurrentUserId), await Users.GetProfileStatsAsync(CurrentUserId));

        private string ResetLink(User user) =>
            PublicLink(Url.Action(nameof(ResetPassword), "Account", new { token = _auth.CreatePasswordResetToken(user) })!);

        // The request Host header is client-controlled, so outside development the configured public address is used.
        private string PublicLink(string path)
        {
            if (string.IsNullOrWhiteSpace(_publicUrl))
            {
                return $"{Request.Scheme}://{Request.Host}{path}";
            }
            if (Request.PathBase.HasValue && path.StartsWith(Request.PathBase.Value!, StringComparison.Ordinal))
            {
                path = path[Request.PathBase.Value!.Length..];
            }
            return _publicUrl.TrimEnd('/') + path;
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
