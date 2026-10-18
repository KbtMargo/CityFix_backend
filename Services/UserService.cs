using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _hasher;
        private readonly TimeProvider _clock;

        public UserService(ApplicationDbContext context, IPasswordHasher hasher, TimeProvider clock)
        {
            _context = context;
            _hasher = hasher;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public async Task<User> GetActorAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.IsBlocked)
            {
                throw new ForbiddenException($"User {userId} no longer exists or is blocked.");
            }
            return user;
        }

        public async Task<UsersPage> GetPageAsync(User actor, string? q, int page)
        {
            var query = _context.Users.AsNoTracking().Include(u => u.Acab).AsQueryable();
            Acab? ownAcab = null;
            if (actor.Role != UserRole.Administrator)
            {
                if (actor.Role != UserRole.AcabAdmin) throw new ForbiddenException($"User {actor.Id} may not list users.");
                ownAcab = actor.AcabId == null ? null : await _context.Acabs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == actor.AcabId);
                query = actor.AcabId == null ? query.Where(u => u.Id == actor.Id) : query.Where(u => u.AcabId == actor.AcabId);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = Search.ContainsPattern(q);
                query = query.Where(u => EF.Functions.ILike(u.FirstName, pattern, "\\")
                    || (u.LastName != null && EF.Functions.ILike(u.LastName, pattern, "\\"))
                    || EF.Functions.ILike(u.Email, pattern, "\\"));
            }

            var users = await query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id).ToPagedResultAsync(page);
            var ids = users.Items.Select(u => u.Id).ToList();

            // A user with appeals or status changes cannot be deleted (their history would go with them).
            var appealCounts = await _context.Appeals.Where(a => ids.Contains(a.AuthorId))
                .GroupBy(a => a.AuthorId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var withHistory = (await _context.AppealUpdates.Where(u => ids.Contains(u.ChangedById))
                .Select(u => u.ChangedById).Distinct().ToListAsync()).ToHashSet();

            var rows = users.Map(u =>
            {
                var appeals = appealCounts.GetValueOrDefault(u.Id);
                var blockedKey = u.Id == actor.Id ? "Users.CannotDeleteSelf"
                    : appeals > 0 || withHistory.Contains(u.Id) ? "Users.CannotDelete"
                    : null;
                return new UserRow(u, appeals, blockedKey);
            });

            return new UsersPage(rows, ownAcab);
        }

        public async Task<User> GetAsync(int id) =>
            await _context.Users.AsNoTracking().Include(u => u.Acab).FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new NotFoundException($"User {id} does not exist.");

        public async Task<(User User, string TemporaryPassword)> CreateAsync(UserInput input, User actor)
        {
            EnsureAdministrator(actor);
            await ValidateRoleAsync(input);
            var password = RandomCodes.TemporaryPassword();
            return (await CreateAccountAsync(input, password), password);
        }

        public async Task<User> CreateAccountAsync(UserInput input, string password)
        {
            var email = EmailAddress.Normalize(input.Email);
            await EnsureEmailFreeAsync(email, null);

            var now = Now;
            var user = new User
            {
                Email = email,
                PasswordHash = _hasher.Hash(password),
                FirstName = input.FirstName.Trim(),
                LastName = Clean(input.LastName),
                Phone = Clean(input.Phone),
                Role = input.Role,
                AcabId = input.AcabId,
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
                // Someone registered the same email between the check and the insert (unique index).
                _context.Entry(user).State = EntityState.Detached;
                if (await EmailTakenAsync(email, null))
                {
                    throw new ServiceValidationException("Email", "Auth.EmailTaken");
                }
                throw;
            }
            return user;
        }

        public async Task UpdateAsync(int id, UserInput input, User actor)
        {
            EnsureAdministrator(actor);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new NotFoundException($"User {id} does not exist.");
            await ValidateRoleAsync(input);

            var email = EmailAddress.Normalize(input.Email);
            await EnsureEmailFreeAsync(email, id);

            // The last administrator (or yourself) cannot lose the role: nobody could manage the system.
            if (user.Role == UserRole.Administrator && input.Role != UserRole.Administrator)
            {
                if (user.Id == actor.Id) throw new ServiceValidationException("Role", "Users.CannotDemoteSelf");
                await EnsureAnotherAdministratorAsync(user.Id);
            }

            user.FirstName = input.FirstName.Trim();
            user.LastName = Clean(input.LastName);
            user.Email = email;
            user.Phone = Clean(input.Phone);
            user.Role = input.Role;
            user.AcabId = input.AcabId;
            user.UpdatedAt = Now;
            await _context.SaveChangesAsync();
        }

        public async Task<string> ResetPasswordAsync(int id, User actor)
        {
            EnsureAdministrator(actor);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new NotFoundException($"User {id} does not exist.");

            var password = RandomCodes.TemporaryPassword();
            user.PasswordHash = _hasher.Hash(password);
            user.UpdatedAt = Now;
            await _context.SaveChangesAsync();
            return password;
        }

        public async Task SetBlockedAsync(int id, bool blocked, User actor)
        {
            EnsureAdministrator(actor);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new NotFoundException($"User {id} does not exist.");
            if (blocked && user.Id == actor.Id)
            {
                throw new ServiceValidationException("", "Users.CannotBlockSelf");
            }
            if (blocked && user.Role == UserRole.Administrator)
            {
                await EnsureAnotherAdministratorAsync(user.Id);
            }

            user.IsBlocked = blocked;
            user.UpdatedAt = Now;
            await _context.SaveChangesAsync();
        }

        public async Task<DeleteCheck> CheckDeleteAsync(int id, User actor)
        {
            EnsureAdministrator(actor);
            var user = await GetAsync(id);
            var appeals = await _context.Appeals.CountAsync(a => a.AuthorId == id);
            var history = await _context.AppealUpdates.AnyAsync(u => u.ChangedById == id);

            var blockedKey = id == actor.Id ? "Users.CannotDeleteSelfText"
                : appeals > 0 || history ? "Users.CannotDeleteText"
                : null;
            return new DeleteCheck(id, user.FullName, appeals, blockedKey);
        }

        public async Task DeleteAsync(int id, User actor)
        {
            var check = await CheckDeleteAsync(id, actor);
            if (check.Blocked)
            {
                throw new ServiceValidationException("", check.BlockedKey!, check.UsageCount);
            }
            await _context.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
        }

        public async Task UpdateProfileAsync(int userId, ProfileInput input)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new NotFoundException($"User {userId} does not exist.");
            var email = EmailAddress.Normalize(input.Email);
            await EnsureEmailFreeAsync(email, userId);

            user.FirstName = input.FirstName.Trim();
            user.LastName = Clean(input.LastName);
            user.Email = email;
            user.Phone = Clean(input.Phone);
            user.UpdatedAt = Now;
            await _context.SaveChangesAsync();
        }

        private static void EnsureAdministrator(User actor)
        {
            if (actor.Role != UserRole.Administrator)
            {
                throw new ForbiddenException($"User {actor.Id} is not an administrator.");
            }
        }

        // A role must be one of the enum values; a head always belongs to an existing association.
        private async Task ValidateRoleAsync(UserInput input)
        {
            if (!Enum.IsDefined(input.Role))
            {
                throw new ServiceValidationException("Role", "Validation.Choose");
            }
            if (input.AcabId != null && !await _context.Acabs.AnyAsync(a => a.Id == input.AcabId))
            {
                throw new ServiceValidationException("AcabId", "Validation.Choose");
            }
            if (input.Role == UserRole.AcabAdmin && input.AcabId == null)
            {
                throw new ServiceValidationException("AcabId", "Validation.HeadNeedsAcab");
            }
        }

        private async Task EnsureAnotherAdministratorAsync(int exceptId)
        {
            if (!await _context.Users.AnyAsync(u => u.Role == UserRole.Administrator && !u.IsBlocked && u.Id != exceptId))
            {
                throw new ServiceValidationException("Role", "Users.LastAdmin");
            }
        }

        private async Task EnsureEmailFreeAsync(string email, int? exceptId)
        {
            if (await EmailTakenAsync(email, exceptId))
            {
                throw new ServiceValidationException("Email", "Auth.EmailTaken");
            }
        }

        // Emails are stored lower-cased, so the unique index is used directly.
        private Task<bool> EmailTakenAsync(string email, int? exceptId) =>
            _context.Users.AnyAsync(u => u.Email == email && u.Id != (exceptId ?? 0));

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
