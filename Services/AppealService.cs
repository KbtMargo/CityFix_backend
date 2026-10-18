using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class AppealService : IAppealService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;

        public AppealService(ApplicationDbContext context, INotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        public async Task<PagedResult<Appeal>> GetFilteredAsync(AppealFilter filter, int userId)
        {
            var actor = await GetUserAsync(userId);
            var query = VisibleTo(actor)
                .Include(a => a.Author)
                .Include(a => a.Category)
                .Include(a => a.Recipient)
                .Include(a => a.Files.OrderBy(f => f.Id))
                .AsNoTracking();

            if (filter.MineOnly)
            {
                query = query.Where(a => a.AuthorId == actor.Id);
            }

            if (!string.IsNullOrWhiteSpace(filter.Q))
            {
                // Case-insensitive "contains" on title and description; % and _ typed by the user are literal.
                var pattern = "%" + filter.Q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                query = query.Where(a => EF.Functions.ILike(a.Title, pattern, "\\")
                    || (a.Description != null && EF.Functions.ILike(a.Description, pattern, "\\")));
            }

            if (filter.From != null)
            {
                var fromUtc = UiFormat.KyivDayStartUtc(filter.From.Value);
                query = query.Where(a => a.CreatedAt >= fromUtc);
            }
            if (filter.To != null)
            {
                var toUtc = UiFormat.KyivDayStartUtc(filter.To.Value.AddDays(1));
                query = query.Where(a => a.CreatedAt < toUtc);
            }
            if (filter.CategoryId != null)
            {
                query = query.Where(a => a.CategoryId == filter.CategoryId);
            }
            if (filter.RecipientId != null)
            {
                query = query.Where(a => a.RecipientId == filter.RecipientId);
            }
            if (filter.AcabId != null)
            {
                query = query.Where(a => a.Author!.AcabId == filter.AcabId);
            }

            query = filter.CurrentTab switch
            {
                AppealFilter.TabNew => query.Where(a => a.Status == AppealStatus.New),
                AppealFilter.TabInProgress => query.Where(a => a.Status == AppealStatus.InProgress),
                AppealFilter.TabClosed => query.Where(a => a.Status == AppealStatus.Done || a.Status == AppealStatus.Rejected),
                _ => query
            };

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToPagedResultAsync(filter.Page);
        }

        public async Task<Appeal?> GetByIdAsync(int id, int userId)
        {
            var actor = await GetUserAsync(userId);
            var appeal = await _context.Appeals
                .Include(a => a.Author)
                .Include(a => a.Category)
                .Include(a => a.Recipient)
                .Include(a => a.Files.OrderBy(f => f.Id))
                .Include(a => a.Updates.OrderBy(u => u.ChangedAt)).ThenInclude(u => u.ChangedBy)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == id);
            if (appeal != null)
            {
                EnsureCanAccess(actor, appeal);
            }
            return appeal;
        }

        public async Task<Appeal> CreateAsync(Appeal appeal, int userId)
        {
            var actor = await GetUserAsync(userId);
            NormalizeLocation(appeal);

            // Never trust author, status or priority from the form.
            appeal.Id = 0;
            appeal.AuthorId = actor.Id;
            appeal.Status = AppealStatus.New;
            appeal.Priority = AppealPriority.Normal;
            appeal.CreatedAt = DateTime.UtcNow;
            appeal.UpdatedAt = appeal.CreatedAt;

            _context.Appeals.Add(appeal);
            await _context.SaveChangesAsync();
            return appeal;
        }

        public async Task<bool> UpdateAsync(Appeal appeal, int userId)
        {
            var actor = await GetUserAsync(userId);
            var existing = await _context.Appeals.Include(a => a.Author).FirstOrDefaultAsync(a => a.Id == appeal.Id);
            if (existing == null)
            {
                return false;
            }
            EnsureCanAccess(actor, existing);
            NormalizeLocation(appeal);

            // Only user-editable fields are copied; author, status and CreatedAt keep their stored values.
            existing.Title = appeal.Title;
            existing.Description = appeal.Description;
            existing.Lat = appeal.Lat;
            existing.Lng = appeal.Lng;
            existing.Address = appeal.Address;
            existing.CategoryId = appeal.CategoryId;
            existing.RecipientId = appeal.RecipientId;
            if (IsStaff(actor))
            {
                existing.Priority = appeal.Priority;
            }

            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateStatusAsync(int appealId, AppealStatus newStatus, string? comment, int changedByUserId)
        {
            var actor = await GetUserAsync(changedByUserId);
            if (!IsStaff(actor))
            {
                throw new UnauthorizedAccessException($"User {actor.Id} may not change appeal statuses.");
            }

            var appeal = await _context.Appeals.Include(a => a.Author).FirstOrDefaultAsync(a => a.Id == appealId)
                ?? throw new KeyNotFoundException($"Appeal {appealId} does not exist.");
            EnsureCanAccess(actor, appeal);

            comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

            // Business rule: closing an appeal as Done must say what was done.
            if (newStatus == AppealStatus.Done && comment == null)
            {
                throw new ServiceValidationException("Comment", "Validation.DoneNeedsComment");
            }
            if (newStatus == appeal.Status && comment == null)
            {
                throw new ServiceValidationException("Status", "Validation.NothingChanged");
            }

            var now = DateTime.UtcNow;
            var oldStatus = appeal.Status;
            _context.AppealUpdates.Add(new AppealUpdate
            {
                AppealId = appeal.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedById = actor.Id,
                Comment = comment,
                ChangedAt = now
            });
            appeal.Status = newStatus;
            appeal.UpdatedAt = now;
            await _context.SaveChangesAsync();

            if (appeal.AuthorId != actor.Id)
            {
                // A comment without a status change is sent as a comment notification (no new status).
                await _notifications.NotifyStatusChangeAsync(appeal, oldStatus == newStatus ? null : newStatus, comment);
            }
        }

        public async Task DeleteAsync(int id, int userId)
        {
            var actor = await GetUserAsync(userId);
            var appeal = await _context.Appeals.Include(a => a.Author).FirstOrDefaultAsync(a => a.Id == id);
            if (appeal == null)
            {
                return;
            }
            EnsureCanAccess(actor, appeal);

            _context.Appeals.Remove(appeal);
            await _context.SaveChangesAsync();
        }

        public async Task<AppealStats> GetStatsAsync(int userId)
        {
            var actor = await GetUserAsync(userId);
            var counts = await VisibleTo(actor)
                .GroupBy(a => a.Status == AppealStatus.Done || a.Status == AppealStatus.Rejected)
                .Select(g => new { Closed = g.Key, Count = g.Count() })
                .ToListAsync();
            return new AppealStats(
                counts.Where(c => !c.Closed).Sum(c => c.Count),
                counts.Where(c => c.Closed).Sum(c => c.Count));
        }

        public async Task<User> GetUserAsync(int userId)
        {
            // Role and association are read from the database, so a changed role applies immediately.
            return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new UnauthorizedAccessException("The signed-in user no longer exists.");
        }

        public async Task<AppealLookups> GetLookupsAsync()
        {
            return new AppealLookups(
                await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                await _context.Recipients.AsNoTracking().OrderBy(r => r.Name).ToListAsync(),
                await _context.Acabs.AsNoTracking().OrderBy(a => a.Name).ToListAsync());
        }

        private IQueryable<Appeal> VisibleTo(User actor) => _context.Appeals.VisibleTo(actor);

        // An appeal needs a point on the map, or an address when no point is chosen.
        private static void NormalizeLocation(Appeal appeal)
        {
            appeal.Address = string.IsNullOrWhiteSpace(appeal.Address) ? null : appeal.Address.Trim();
            appeal.Description = string.IsNullOrWhiteSpace(appeal.Description) ? null : appeal.Description.Trim();
            appeal.Title = appeal.Title.Trim();

            if (appeal.Lat == null || appeal.Lng == null)
            {
                // Half a coordinate pair is meaningless.
                appeal.Lat = null;
                appeal.Lng = null;
            }
            else
            {
                appeal.Lat = Math.Round(appeal.Lat.Value, 5);
                appeal.Lng = Math.Round(appeal.Lng.Value, 5);
            }

            if (appeal.Address == null && appeal.Lat == null)
            {
                throw new ServiceValidationException(nameof(Appeal.Address), "Validation.LocationRequired");
            }
        }

        private static bool IsStaff(User actor) =>
            actor.Role is UserRole.Administrator or UserRole.AcabAdmin;

        private static void EnsureCanAccess(User actor, Appeal appeal)
        {
            var allowed = actor.Role == UserRole.Administrator
                || appeal.AuthorId == actor.Id
                || (actor.Role == UserRole.AcabAdmin && actor.AcabId != null && appeal.Author?.AcabId == actor.AcabId);

            if (!allowed)
            {
                throw new UnauthorizedAccessException($"User {actor.Id} may not access appeal {appeal.Id}.");
            }
        }
    }
}
