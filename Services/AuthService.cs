using System.Security.Claims;
using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _users;
        private readonly IAcabService _acabs;
        private readonly IPasswordHasher _hasher;
        private readonly TimeProvider _clock;

        public AuthService(ApplicationDbContext context, IUserService users, IAcabService acabs, IPasswordHasher hasher, TimeProvider clock)
        {
            _context = context;
            _users = users;
            _acabs = acabs;
            _hasher = hasher;
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

            try
            {
                var user = await _users.CreateAccountAsync(
                    new UserInput(request.FirstName, request.LastName, request.Email, request.Phone, role, acabId),
                    request.Password);
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
