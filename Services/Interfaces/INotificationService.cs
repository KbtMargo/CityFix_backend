using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    // Notifications are added to the current unit of work and saved together with the change that caused them.
    public interface INotificationService
    {
        // To the author: a status change (newStatus) or a staff comment (newStatus null).
        void AddStatusChanged(Appeal appeal, AppealStatus? newStatus, string? comment);

        // To the heads of the appeal's association.
        Task AddNewAppealAsync(Appeal appeal);

        // To the author and, when the association is now responsible, its heads. Skips the actor.
        Task AddAssignedAsync(Appeal appeal, string responsibleName, int actorId);

        // Newest first; the appeal is included for the notification text.
        Task<List<Notification>> GetForUserAsync(int userId, bool unreadOnly);

        Task<int> CountUnreadAsync(int userId);

        // Marks one of the user's notifications as read and returns it; null if it isn't theirs.
        Task<Notification?> MarkReadAsync(int id, int userId);

        Task MarkAllReadAsync(int userId);
    }
}
