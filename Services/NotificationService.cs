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

        public Task AddStatusChangedAsync(Appeal appeal, AppealStatus? newStatus, string? comment) =>
            AddAsync(new[] { appeal.AuthorId }, appeal, NotificationType.StatusChanged, newStatus, comment);

        public async Task AddNewAppealAsync(Appeal appeal)
        {
            var heads = await HeadsOfAsync(appeal.AcabId);
            heads.Remove(appeal.AuthorId);
            await AddAsync(heads, appeal, NotificationType.NewAppeal, null, null);
        }

        public async Task AddAssignedAsync(Appeal appeal, string responsibleName, string? responsibleNameEn, int actorId)
        {
            var recipients = new HashSet<int> { appeal.AuthorId };
            if (appeal.ResponsibleKind == ResponsibleKind.Association)
            {
                recipients.UnionWith(await HeadsOfAsync(appeal.AcabId));
            }
            recipients.Remove(actorId);

            await AddAsync(recipients, appeal, NotificationType.Assigned, null, responsibleName, responsibleNameEn);
        }

        public Task<List<Notification>> GetForUserAsync(int userId, bool unreadOnly)
        {
            var query = _context.Notifications.AsNoTracking()
                .Include(n => n.Appeal)
                .Where(n => n.UserId == userId && n.InApp);
            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }
            return query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToListAsync();
        }

        public Task<int> CountUnreadAsync(int userId) =>
            _context.Notifications.CountAsync(n => n.UserId == userId && n.InApp && !n.IsRead);

        public async Task<Notification?> MarkReadAsync(int id, int userId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && n.InApp);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return notification;
        }

        public Task MarkAllReadAsync(int userId) =>
            _context.Notifications
                .Where(n => n.UserId == userId && n.InApp && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        private async Task AddAsync(IEnumerable<int> userIds, Appeal appeal, NotificationType type, AppealStatus? newStatus, string? message, string? messageEn = null)
        {
            var ids = userIds.ToList();
            if (ids.Count == 0) return;

            var recipients = await _context.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.NotifyInApp, Email = u.NotifyEmail && u.EmailConfirmed, Sms = u.NotifySms && u.Phone != null })
                .ToListAsync();

            foreach (var user in recipients.Where(u => u.NotifyInApp || u.Email || u.Sms))
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    AppealId = appeal.Id,
                    Type = type,
                    NewStatus = newStatus,
                    Message = message?.Trim() ?? "",
                    MessageEn = messageEn,
                    InApp = user.NotifyInApp,
                    IsRead = !user.NotifyInApp,
                    EmailState = user.Email ? DeliveryState.Pending : DeliveryState.None,
                    SmsState = user.Sms ? DeliveryState.Pending : DeliveryState.None,
                    CreatedAt = _clock.GetUtcNow().UtcDateTime
                });
            }
        }

        private async Task<List<int>> HeadsOfAsync(int? acabId) => acabId == null
            ? new List<int>()
            : await _context.Users
                .Where(u => u.AcabId == acabId && u.Role == UserRole.AcabAdmin && !u.IsBlocked)
                .Select(u => u.Id)
                .ToListAsync();
    }
}
