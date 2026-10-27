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
using Microsoft.Extensions.Caching.Memory;
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
        private readonly ITimeLimitedDataProtector _resetTokens;
        private readonly EmailOptions _emailOptions;
        private readonly TimeProvider _clock;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthService> _logger;

        public const int MaxFailedLogins = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan ConfirmationCooldown = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

        public AuthService(ApplicationDbContext context, IUserService users, IAcabService acabs, IPasswordHasher hasher,
            IEmailSender email, IDataProtectionProvider dataProtection, IOptions<EmailOptions> emailOptions, TimeProvider clock,
            IMemoryCache cache, ILogger<AuthService> logger)
        {
            _context = context;
            _users = users;
            _acabs = acabs;
            _hasher = hasher;
            _email = email;
            _emailTokens = dataProtection.CreateProtector("CityFix.EmailConfirmation").ToTimeLimitedDataProtector();
            _resetTokens = dataProtection.CreateProtector("CityFix.PasswordReset").ToTimeLimitedDataProtector();
            _emailOptions = emailOptions.Value;
            _clock = clock;
            _cache = cache;
            _logger = logger;
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

            var email = EmailAddress.Normalize(request.Email);

            // Answering "e-mail taken" would tell anyone which addresses have accounts. The caller treats this like a
            // normal sign-up and mails the owner instead; hashing keeps the response time the same.
            var existing = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email && u.EmailConfirmed);
            if (existing != null)
            {
                _hasher.Hash(request.Password);
                return new RegisterResult(null, new Dictionary<string, string>(), existing);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Users.Where(u => u.Email == email && !u.EmailConfirmed).ExecuteDeleteAsync();

            try
            {
                var user = await _users.CreateAccountAsync(
                    new UserInput(request.FirstName, request.LastName, request.Email, request.Phone, role, acabId),
                    request.Password,
                    emailConfirmed: false,
                    language: CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

                // A head code admits one head only; a parallel sign-up with the same code loses here and rolls back.
                if (role == UserRole.AcabAdmin && !await _acabs.ConsumeHeadCodeAsync(acabId!.Value, request.InvitationCode!))
                {
                    return Failed(nameof(RegisterRequest.InvitationCode), "Auth.CodeNotFound");
                }

                await transaction.CommitAsync();
                return new RegisterResult(user, new Dictionary<string, string>());
            }
            catch (ServiceValidationException ex)
            {
                return Failed(ex.Field, ex.MessageKey);
            }
        }

        public async Task<AssociationChange> JoinAssociationAsync(int userId, string code)
        {
            var user = await _context.Users.Include(u => u.Acab).FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new NotFoundException($"User {userId} does not exist.");
            EnsureCanChangeAssociation(user);

            var found = await _acabs.FindByCodeAsync(code)
                ?? throw new ServiceValidationException("Code", "Auth.CodeNotFound");
            var (acab, role) = found;
            if (user.AcabId == acab.Id && user.Role == role)
            {
                throw new ServiceValidationException("Code", "Profile.Acab.AlreadyThere");
            }

            var previous = new AssociationChange(user.Acab?.Name, user.Role, acab.Name, role);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // Appeals filed earlier keep their association, so its head goes on handling them; only new ones go to the new one.
            user.AcabId = acab.Id;
            user.Role = role;
            user.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();

            // A head code admits one head only, as at sign-up.
            if (role == UserRole.AcabAdmin && !await _acabs.ConsumeHeadCodeAsync(acab.Id, code))
            {
                throw new ServiceValidationException("Code", "Auth.CodeNotFound");
            }
            await transaction.CommitAsync();
            return previous;
        }

        public async Task<AssociationChange> LeaveAssociationAsync(int userId)
        {
            var user = await _context.Users.Include(u => u.Acab).FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new NotFoundException($"User {userId} does not exist.");
            EnsureCanChangeAssociation(user);
            if (user.AcabId == null)
            {
                throw new ServiceValidationException("Code", "Profile.Acab.None");
            }

            // A head steps down and stays a resident of the house; a resident moves out of it.
            var change = user.Role == UserRole.AcabAdmin
                ? new AssociationChange(user.Acab?.Name, user.Role, user.Acab?.Name, UserRole.Resident)
                : new AssociationChange(user.Acab?.Name, user.Role, null, UserRole.Resident);
            if (user.Role == UserRole.Resident) user.AcabId = null;
            user.Role = UserRole.Resident;
            user.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return change;
        }

        private static void EnsureCanChangeAssociation(User user)
        {
            if (user.Role is not (UserRole.Resident or UserRole.AcabAdmin))
            {
                throw new ForbiddenException($"User {user.Id} manages associations through the admin pages.");
            }
        }

        public async Task<LoginResult> LoginAsync(string email, string password)
        {
            var normalized = EmailAddress.Normalize(email);
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized);
            if (user == null)
            {
                _hasher.VerifyDummy(password);
                return new LoginResult(null, false);
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            if (user.LockoutEnd > now)
            {
                _hasher.VerifyDummy(password);
                return new LoginResult(null, true);
            }

            if (!_hasher.Verify(password, user.PasswordHash))
            {
                // One atomic UPDATE so parallel guesses cannot slip past the counter.
                var lockUntil = now + LockoutDuration;
                await _context.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LockoutEnd, u => u.FailedLoginCount + 1 >= MaxFailedLogins ? lockUntil : u.LockoutEnd)
                    .SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1 >= MaxFailedLogins ? 0 : u.FailedLoginCount + 1));
                return new LoginResult(null, false);
            }

            if (user.FailedLoginCount > 0 || user.LockoutEnd != null)
            {
                await _context.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.FailedLoginCount, 0)
                    .SetProperty(u => u.LockoutEnd, (DateTime?)null));
            }
            return new LoginResult(user.IsBlocked ? null : user, false);
        }

        public string CreateEmailToken(int userId, string email) =>
            _emailTokens.Protect($"{userId}\n{EmailAddress.Normalize(email)}", _emailOptions.ConfirmationLifetime);

        public async Task SendConfirmationAsync(User user, string email, string link)
        {
            if (Throttled("confirm", email)) return;

            var app = SharedText.Get("App.Name");
            var hours = (int)_emailOptions.ConfirmationLifetime.TotalHours;
            var changing = string.Equals(user.PendingEmail, email, StringComparison.OrdinalIgnoreCase);
            var message = new EmailTemplate
            {
                Subject = SharedText.Format("Email.Confirm.Subject", app),
                Preheader = SharedText.Format(changing ? "Email.Confirm.ChangePreheader" : "Email.Confirm.Preheader", app),
                Heading = EmailTemplate.Greeting(user.FirstName),
                Paragraphs = { SharedText.Format(changing ? "Email.Confirm.ChangeText" : "Email.Confirm.Text", app, email) },
                ButtonText = SharedText.Get("Email.Confirm.Button"),
                ButtonUrl = link,
                Notes =
                {
                    SharedText.Format("Email.Confirm.Expires", hours),
                    SharedText.Get(changing ? "Email.Confirm.ChangeIgnore" : "Email.Confirm.Ignore")
                }
            }.Render();
            await _email.SendAsync(email, message);
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
                if (await _context.Users.AnyAsync(u => u.Email == email && u.Id != userId))
                {
                    return (EmailConfirmation.EmailTaken, user);
                }
                user.Email = email;
                user.PendingEmail = null;
            }
            else if (user.Email != email)
            {
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
                return (EmailConfirmation.EmailTaken, user);
            }
            return (EmailConfirmation.Confirmed, user);
        }

        public Task<User?> FindUnconfirmedAsync(string email)
        {
            var normalized = EmailAddress.Normalize(email);
            return _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized && !u.EmailConfirmed && !u.IsBlocked);
        }

        public async Task<User?> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !_hasher.Verify(currentPassword, user.PasswordHash))
            {
                return null;
            }

            user.PasswordHash = _hasher.Hash(newPassword);
            user.SecurityStamp = SecurityStamps.New();
            user.MustChangePassword = false;
            user.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> VerifyPasswordAsync(int userId, string password)
        {
            var hash = await _context.Users.Where(u => u.Id == userId).Select(u => u.PasswordHash).FirstOrDefaultAsync();
            return hash != null && _hasher.Verify(password, hash);
        }

        public ClaimsPrincipal CreatePrincipal(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Role, user.Role.ToString()),
                new(SecurityStamps.ClaimType, user.SecurityStamp)
            };
            if (user.MustChangePassword)
            {
                claims.Add(new Claim(SecurityStamps.MustChangePasswordClaim, "true"));
            }
            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        public async Task<ClaimsPrincipal?> RefreshPrincipalAsync(ClaimsPrincipal principal)
        {
            if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                return null;
            }

            var user = await _users.FindActorAsync(id);
            if (user == null || principal.FindFirstValue(SecurityStamps.ClaimType) != user.SecurityStamp)
            {
                return null;
            }

            var unchanged = principal.FindFirstValue(ClaimTypes.Role) == user.Role.ToString()
                && principal.FindFirstValue(ClaimTypes.Name) == user.FullName
                && principal.FindFirstValue(ClaimTypes.Email) == user.Email
                && principal.HasClaim(SecurityStamps.MustChangePasswordClaim, "true") == user.MustChangePassword;
            return unchanged ? principal : CreatePrincipal(user);
        }

        // The token carries the security stamp, which a successful reset (or any password change) replaces,
        // so each link works once and dies as soon as the password changes another way.
        public string CreatePasswordResetToken(User user) =>
            _resetTokens.Protect($"{user.Id}\n{user.SecurityStamp}", PasswordResetLifetime);

        public async Task<User?> FindByResetTokenAsync(string token)
        {
            string payload;
            try
            {
                payload = _resetTokens.Unprotect(token);
            }
            catch (CryptographicException)
            {
                return null;
            }

            var parts = payload.Split('\n');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var userId)) return null;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            return user == null || user.IsBlocked || user.SecurityStamp != parts[1] ? null : user;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var user = await FindByResetTokenAsync(token);
            if (user == null) return false;

            user.PasswordHash = _hasher.Hash(newPassword);
            user.SecurityStamp = SecurityStamps.New();
            user.MustChangePassword = false;
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            // Following a link from the mailbox proves the address belongs to the user.
            if (!user.EmailConfirmed && user.PendingEmail == null) user.EmailConfirmed = true;
            user.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return true;
        }

        public Task<User?> FindForPasswordResetAsync(string email)
        {
            var normalized = EmailAddress.Normalize(email);
            return _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized && !u.IsBlocked);
        }

        public Task SendPasswordResetAsync(User user, string link) => SendResetLetterAsync(user, link, "Email.Reset.Text");

        public Task SendAccountExistsAsync(User user, string resetLink) => SendResetLetterAsync(user, resetLink, "Email.Exists.Text");

        private async Task SendResetLetterAsync(User user, string link, string textKey)
        {
            if (Throttled("reset", user.Email)) return;

            var app = SharedText.Get("App.Name");
            var message = new EmailTemplate
            {
                Subject = SharedText.Format("Email.Reset.Subject", app),
                Preheader = SharedText.Format("Email.Reset.Preheader", app),
                Heading = EmailTemplate.Greeting(user.FirstName),
                Paragraphs = { SharedText.Format(textKey, app) },
                ButtonText = SharedText.Get("Email.Reset.Button"),
                ButtonUrl = link,
                Notes =
                {
                    SharedText.Format("Email.Reset.Expires", (int)PasswordResetLifetime.TotalMinutes),
                    SharedText.Get("Email.Reset.Ignore")
                }
            }.Render();
            await _email.SendAsync(user.Email, message);
            _logger.LogInformation("Password letter sent to {Email}.", user.Email);
        }

        // Sign-up, "resend" and "forgot password" can be triggered by anyone for any address; cap letters per address to stop mail bombing.
        private bool Throttled(string kind, string email)
        {
            var key = $"mail:{kind}:{EmailAddress.Normalize(email)}";
            if (_cache.TryGetValue(key, out _))
            {
                _logger.LogInformation("{Kind} letter to {Email} skipped: one was sent less than {Cooldown} ago.", kind, email, ConfirmationCooldown);
                return true;
            }
            _cache.Set(key, true, ConfirmationCooldown);
            return false;
        }

        private static RegisterResult Failed(string field, string key) =>
            new(null, new Dictionary<string, string> { [field] = key });
    }

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
