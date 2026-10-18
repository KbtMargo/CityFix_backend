using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task NotifyStatusChangeAsync(Appeal appeal, AppealStatus? newStatus, string? comment = null)
        {
            // The text is rendered from Type + NewStatus in the reader's language; only the comment is stored as text.
            _context.Notifications.Add(new Notification
            {
                UserId = appeal.AuthorId,
                AppealId = appeal.Id,
                Type = NotificationType.StatusChanged,
                NewStatus = newStatus,
                Message = comment?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
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
    }
}
