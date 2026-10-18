using System.Security.Claims;
using System.Security.Cryptography;
using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class AuthService : IAuthService
    {
        // No 0/O or 1/l/I, so a temporary password can be read out or copied by hand.
        private const string TemporaryPasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        private readonly ApplicationDbContext _context;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
        {
            var errors = new Dictionary<string, string>();
            var email = NormalizeEmail(request.Email);

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
            {
                errors[nameof(RegisterRequest.Email)] = "Auth.EmailTaken";
            }

            // An invitation code links the new user to an association as its representative.
            Acab? acab = null;
            if (!string.IsNullOrWhiteSpace(request.InvitationCode))
            {
                var code = request.InvitationCode.Trim().ToLower();
                acab = await _context.Acabs.FirstOrDefaultAsync(a => a.InvitationCode.ToLower() == code);
                if (acab == null)
                {
                    errors[nameof(RegisterRequest.InvitationCode)] = "Auth.CodeNotFound";
                }
            }

            if (errors.Count > 0)
            {
                return new RegisterResult(null, errors);
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                Email = email,
                PasswordHash = HashPassword(request.Password),
                FirstName = request.FirstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                Role = acab != null ? UserRole.AcabAdmin : UserRole.Resident,
                AcabId = acab?.Id,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Users.Add(user);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Someone registered the same email between our check and the insert (unique index).
                _context.Entry(user).State = EntityState.Detached;
                if (!await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
                {
                    throw;
                }
                return new RegisterResult(null, new Dictionary<string, string> { [nameof(RegisterRequest.Email)] = "Auth.EmailTaken" });
            }

            return new RegisterResult(user, errors);
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            var normalized = NormalizeEmail(email);
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
            if (user == null)
            {
                return null;
            }

            return VerifyPassword(password, user.PasswordHash) ? user : null;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !VerifyPassword(currentPassword, user.PasswordHash))
            {
                return false;
            }

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static bool VerifyPassword(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // Not a BCrypt hash (rows typed in before hashing existed): this account cannot log in.
                return false;
            }
        }

        public ClaimsPrincipal CreatePrincipal(User user)
        {
            var fullName = string.IsNullOrWhiteSpace(user.LastName) ? user.FirstName : $"{user.FirstName} {user.LastName}";
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, fullName),
                new(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }

        public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        public string GenerateTemporaryPassword() => RandomNumberGenerator.GetString(TemporaryPasswordAlphabet, 12);

        public string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
