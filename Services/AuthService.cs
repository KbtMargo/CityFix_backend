using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _users;
        private readonly IAcabService _acabs;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly ITimeLimitedDataProtector _emailTokens;
        private readonly EmailOptions _emailOptions;
        private readonly TimeProvider _clock;

        public AuthService(ApplicationDbContext context, IUserService users, IAcabService acabs, IPasswordHasher hasher,
            IEmailSender email, IDataProtectionProvider dataProtection, IOptions<EmailOptions> emailOptions, TimeProvider clock)
        {
            _context = context;
            _users = users;
            _acabs = acabs;
            _hasher = hasher;
            _email = email;
            _emailTokens = dataProtection.CreateProtector("CityFix.EmailConfirmation").ToTimeLimitedDataProtector();
            _emailOptions = emailOptions.Value;
            _clock = clock;
        }

        public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
        {
            var role = UserRole.Resident;
            int? acabId = null;
            if (!string.IsNullOrWhiteSpace(request.InvitationCode))
            {
                var found = await _acabs.FindByCodeAsync(request.InvitationCode);
                if (found == null)
                {
                    return Failed(nameof(RegisterRequest.InvitationCode), "Auth.CodeNotFound");
                }
                (role, acabId) = (found.Value.Role, found.Value.Acab.Id);
            }

            // An unconfirmed account has never signed in (so it has no appeals): registering again with the
            // same email replaces it. Nobody can hold on to someone else's address by not confirming it.
            var email = EmailAddress.Normalize(request.Email);
            await _context.Users.Where(u => u.Email == email && !u.EmailConfirmed).ExecuteDeleteAsync();

            try
            {
                var user = await _users.CreateAccountAsync(
                    new UserInput(request.FirstName, request.LastName, request.Email, request.Phone, role, acabId),
                    request.Password,
                    emailConfirmed: false,
                    language: CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                return new RegisterResult(user, new Dictionary<string, string>());
            }
            catch (ServiceValidationException ex)
            {
                return Failed(ex.Field, ex.MessageKey);
            }
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            var normalized = EmailAddress.Normalize(email);
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized);
            if (user == null)
            {
                _hasher.VerifyDummy(password);
                return null;
            }
            return _hasher.Verify(password, user.PasswordHash) && !user.IsBlocked ? user : null;
        }

        public string CreateEmailToken(int userId, string email) =>
            _emailTokens.Protect($"{userId}\n{EmailAddress.Normalize(email)}", _emailOptions.ConfirmationLifetime);

        public async Task SendConfirmationAsync(User user, string email, string link)
        {
            var hours = (int)_emailOptions.ConfirmationLifetime.TotalHours;
            var body = string.Join("\n",
                SharedText.Format("Email.Confirm.Greeting", user.FirstName),
                "",
                SharedText.Format("Email.Confirm.Text", SharedText.Get("App.Name")),
                "",
                link,
                "",
                SharedText.Format("Email.Confirm.Expires", hours),
                SharedText.Get("Email.Confirm.Ignore"));
            await _email.SendAsync(email, SharedText.Format("Email.Confirm.Subject", SharedText.Get("App.Name")), body);
        }

        public async Task<(EmailConfirmation Result, User? User)> ConfirmEmailAsync(string token)
        {
            string payload;
            try
            {
                payload = _emailTokens.Unprotect(token);
            }
            catch (CryptographicException)
            {
                // Tampered with, expired, or made with keys that no longer exist.
                return (EmailConfirmation.Invalid, null);
            }

            var parts = payload.Split('\n');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var userId))
            {
                return (EmailConfirmation.Invalid, null);
            }
            var email = parts[1];

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.IsBlocked)
            {
                return (EmailConfirmation.Invalid, null);
            }

            if (user.PendingEmail == email)
            {
                // A new address from the profile: it becomes the login once confirmed (if still free).
                if (await _context.Users.AnyAsync(u => u.Email == email && u.Id != userId))
                {
                    return (EmailConfirmation.EmailTaken, user);
                }
                user.Email = email;
                user.PendingEmail = null;
            }
            else if (user.Email != email)
            {
                // A link for an address the account no longer uses.
                return (EmailConfirmation.Invalid, null);
            }

            user.EmailConfirmed = true;
            user.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Someone took the address between the check and the update (unique index).
                return (EmailConfirmation.EmailTaken, user);
            }
            return (EmailConfirmation.Confirmed, user);
        }

        public Task<User?> FindUnconfirmedAsync(string email)
        {
            var normalized = EmailAddress.Normalize(email);
            return _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized && !u.EmailConfirmed && !u.IsBlocked);
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !_hasher.Verify(currentPassword, user.PasswordHash))
            {
                return false;
            }

            user.PasswordHash = _hasher.Hash(newPassword);
            user.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return true;
        }

        public ClaimsPrincipal CreatePrincipal(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Role, user.Role.ToString())
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        public async Task<ClaimsPrincipal?> RefreshPrincipalAsync(ClaimsPrincipal principal)
        {
            if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                return null;
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null || user.IsBlocked)
            {
                return null;
            }

            // Same role, name and email: keep the cookie as it is.
            var unchanged = principal.FindFirstValue(ClaimTypes.Role) == user.Role.ToString()
                && principal.FindFirstValue(ClaimTypes.Name) == user.FullName
                && principal.FindFirstValue(ClaimTypes.Email) == user.Email;
            return unchanged ? principal : CreatePrincipal(user);
        }

        private static RegisterResult Failed(string field, string key) =>
            new(null, new Dictionary<string, string> { [field] = key });
    }

    // Wired into the cookie handler (Program.cs): every request re-reads the account, so a blocked or
    // deleted user is signed out and a changed role applies at once (not only when the cookie expires).
    public static class CookieValidator
    {
        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            if (context.Principal == null) return;

            var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
            var current = await auth.RefreshPrincipalAsync(context.Principal);
            if (current == null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            else if (!ReferenceEquals(current, context.Principal))
            {
                context.ReplacePrincipal(current);
                context.ShouldRenew = true;
            }
        }
    }
}
