using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _clock;

        public NotificationService(ApplicationDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        // The text is rendered from Type + NewStatus in the reader's language; only free text is stored.
        public void AddStatusChanged(Appeal appeal, AppealStatus? newStatus, string? comment) =>
            Add(appeal.AuthorId, appeal, NotificationType.StatusChanged, newStatus, comment);

        public async Task AddNewAppealAsync(Appeal appeal)
        {
            foreach (var headId in await HeadsOfAsync(appeal.AcabId))
            {
                if (headId != appeal.AuthorId)
                {
                    Add(headId, appeal, NotificationType.NewAppeal, null, null);
                }
            }
        }

        public async Task AddAssignedAsync(Appeal appeal, string responsibleName, int actorId)
        {
            var recipients = new HashSet<int> { appeal.AuthorId };
            if (appeal.ResponsibleKind == ResponsibleKind.Association)
            {
                recipients.UnionWith(await HeadsOfAsync(appeal.AcabId));
            }
            recipients.Remove(actorId);

            foreach (var userId in recipients)
            {
                Add(userId, appeal, NotificationType.Assigned, null, responsibleName);
            }
        }

        public Task<List<Notification>> GetForUserAsync(int userId, bool unreadOnly)
        {
            var query = _context.Notifications.AsNoTracking()
                .Include(n => n.Appeal)
                .Where(n => n.UserId == userId);
            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }
            return query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToListAsync();
        }

        public Task<int> CountUnreadAsync(int userId) =>
            _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task<Notification?> MarkReadAsync(int id, int userId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return notification;
        }

        public Task MarkAllReadAsync(int userId) =>
            _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        private void Add(int userId, Appeal appeal, NotificationType type, AppealStatus? newStatus, string? message) =>
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                AppealId = appeal.Id,
                Type = type,
                NewStatus = newStatus,
                Message = message?.Trim() ?? "",
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            });

        private async Task<List<int>> HeadsOfAsync(int? acabId) => acabId == null
            ? new List<int>()
            : await _context.Users
                .Where(u => u.AcabId == acabId && u.Role == UserRole.AcabAdmin && !u.IsBlocked)
                .Select(u => u.Id)
                .ToListAsync();
    }
}
