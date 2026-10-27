using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface INotificationService
    {
        Task AddStatusChangedAsync(Appeal appeal, AppealStatus? newStatus, string? comment);

        Task AddNewAppealAsync(Appeal appeal);

        Task AddAssignedAsync(Appeal appeal, string responsibleName, string? responsibleNameEn, int actorId);

        Task AddSupportersStatusChangedAsync(Appeal appeal, AppealStatus newStatus, int actorId);

        /// <summary>Notifies whoever processes the appeal: the association's heads, or the administrators without one.</summary>
        Task AddForHeadsAsync(Appeal appeal, NotificationType type, string? message, int? actorId);

        Task<PagedResult<Notification>> GetForUserAsync(int userId, bool unreadOnly, int page);

        Task<int> DeleteOldAsync(TimeSpan readAge, TimeSpan anyAge);

        Task<int> CountUnreadAsync(int userId);

        Task<Notification?> MarkReadAsync(int id, int userId);

        Task MarkAllReadAsync(int userId);
    }
}
