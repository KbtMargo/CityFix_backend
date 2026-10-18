using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface INotificationService
    {
        // Tells the appeal's author that its status changed (newStatus) or that staff added a comment (newStatus null).
        Task NotifyStatusChangeAsync(Appeal appeal, AppealStatus? newStatus, string? comment = null);

        // Newest first; the appeal is included for the notification text.
        Task<List<Notification>> GetForUserAsync(int userId, bool unreadOnly);

        Task<int> CountUnreadAsync(int userId);

        // Marks one of the user's notifications as read and returns it; null if it isn't theirs.
        Task<Notification?> MarkReadAsync(int id, int userId);

        Task MarkAllReadAsync(int userId);
    }
}
