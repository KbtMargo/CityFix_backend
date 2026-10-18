using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    // Reference data for the appeal form and the filters popover.
    public record AppealLookups(
        IReadOnlyList<Category> Categories,
        IReadOnlyList<Recipient> Recipients,
        IReadOnlyList<Acab> Acabs);

    // Sidebar stats box: open = New + InProgress, closed = Done + Rejected.
    public record AppealStats(int Open, int Closed);

    // Access rules (enforced here, not only in controllers):
    //  - Resident: sees and manages only appeals they authored.
    //  - AcabAdmin: also appeals authored by members of their own association.
    //  - Administrator: everything.
    // Only AcabAdmin/Administrator ("staff") change Status (UpdateStatusAsync) and Priority (UpdateAsync).
    // Methods taking an appeal id throw UnauthorizedAccessException when the user may not access it.
    public interface IAppealService
    {
        Task<PagedResult<Appeal>> GetFilteredAsync(AppealFilter filter, int userId);

        // With author, category, recipient, photos and status history. Null if it doesn't exist.
        Task<Appeal?> GetByIdAsync(int id, int userId);

        // The author is always the given user; status starts as New and priority as Normal.
        Task<Appeal> CreateAsync(Appeal appeal, int userId);

        // Copies the fields this user may edit; false if the appeal doesn't exist.
        Task<bool> UpdateAsync(Appeal appeal, int userId);

        // Records an AppealUpdate and notifies the author. Moving to Done requires a comment
        // (ServiceValidationException on "Comment"). Staff only.
        Task UpdateStatusAsync(int appealId, AppealStatus newStatus, string? comment, int changedByUserId);

        Task DeleteAsync(int id, int userId);

        Task<AppealStats> GetStatsAsync(int userId);

        Task<User> GetUserAsync(int userId);

        Task<AppealLookups> GetLookupsAsync();
    }
}
