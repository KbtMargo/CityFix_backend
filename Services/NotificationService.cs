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
            var heads = await HeadsOrAdministratorsAsync(appeal.AcabId);
            heads.Remove(appeal.AuthorId);
            await AddAsync(heads, appeal, NotificationType.NewAppeal, null, null);
        }

        public async Task AddAssignedAsync(Appeal appeal, string responsibleName, string? responsibleNameEn, int actorId)
        {
            var recipients = new HashSet<int> { appeal.AuthorId };
            if (appeal.ResponsibleKind == ResponsibleKind.Association)
            {
                recipients.UnionWith(await HeadsOrAdministratorsAsync(appeal.AcabId));
            }
            recipients.Remove(actorId);

            await AddAsync(recipients, appeal, NotificationType.Assigned, null, responsibleName, responsibleNameEn);
        }

        public async Task AddSupportersStatusChangedAsync(Appeal appeal, AppealStatus newStatus, int actorId)
        {
            // Supporters get the status only: the staff comment is addressed to the author and may be personal.
            var supporters = await _context.AppealSupports
                .Where(s => s.AppealId == appeal.Id && s.UserId != actorId && s.UserId != appeal.AuthorId)
                .Select(s => s.UserId)
                .ToListAsync();
            await AddAsync(supporters, appeal, NotificationType.StatusChanged, newStatus, null);
        }

        public async Task AddForHeadsAsync(Appeal appeal, NotificationType type, string? message, int? actorId)
        {
            // Appeals outside an association are processed by the administrators.
            var heads = appeal.AcabId != null ? await HeadsOrAdministratorsAsync(appeal.AcabId) : await AdministratorsAsync();
            if (actorId != null) heads.Remove(actorId.Value);
            await AddAsync(heads, appeal, type, null, message);
        }

        public const int PageSize = 20;

        public Task<PagedResult<Notification>> GetForUserAsync(int userId, bool unreadOnly, int page)
        {
            var query = _context.Notifications.AsNoTracking()
                .Include(n => n.Appeal)
                .Where(n => n.UserId == userId && n.InApp);
            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }
            return query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToPagedResultAsync(page, PageSize);
        }

        public Task<int> DeleteOldAsync(TimeSpan readAge, TimeSpan anyAge)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var readBefore = now - readAge;
            var anyBefore = now - anyAge;
            // Rows still waiting for an e-mail copy are kept until the dispatcher settles them.
            return _context.Notifications
                .Where(n => n.EmailState != DeliveryState.Pending)
                .Where(n => (n.IsRead && n.CreatedAt < readBefore) || n.CreatedAt < anyBefore)
                .ExecuteDeleteAsync();
        }

        public Task<int> CountUnreadAsync(int userId) =>
            _context.Notifications.CountAsync(n => n.UserId == userId && n.InApp && !n.IsRead);

        public async Task<Notification?> MarkReadAsync(int id, int userId)
        {
            var notification = await _context.Notifications.Include(n => n.Appeal)
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && n.InApp);
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
                .Select(u => new { u.Id, u.NotifyInApp, Email = u.NotifyEmail && u.EmailConfirmed })
                .ToListAsync();

            foreach (var user in recipients.Where(u => u.NotifyInApp || u.Email))
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
                    CreatedAt = _clock.GetUtcNow().UtcDateTime,
                    NextAttemptAt = _clock.GetUtcNow().UtcDateTime
                });
            }
        }

        // While an association has no head (the old one stepped down, the new one has not joined yet) its appeals must not
        // go unnoticed, so the administrators get what the head would have.
        private async Task<List<int>> HeadsOrAdministratorsAsync(int? acabId)
        {
            var heads = await HeadsOfAsync(acabId);
            return heads.Count > 0 || acabId == null ? heads : await AdministratorsAsync();
        }

        private Task<List<int>> AdministratorsAsync() =>
            _context.Users.Where(u => u.Role == UserRole.Administrator && !u.IsBlocked).Select(u => u.Id).ToListAsync();

        private async Task<List<int>> HeadsOfAsync(int? acabId) => acabId == null
            ? new List<int>()
            : await _context.Users
                .Where(u => u.AcabId == acabId && u.Role == UserRole.AcabAdmin && !u.IsBlocked)
                .Select(u => u.Id)
                .ToListAsync();
    }
}
