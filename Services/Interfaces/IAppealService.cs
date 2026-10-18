using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    // Appeals. Access rules come from AppealPolicy and are enforced here, not only in controllers.
    // Methods taking an appeal id throw NotFoundException / ForbiddenException, and
    // ServiceValidationException when a business rule is broken.
    public interface IAppealService
    {
        Task<AppealPage> GetFilteredAsync(AppealFilter filter, User actor);

        // The appeal with author, category, responsible party, photos and history, plus what the actor may do.
        Task<AppealDetails> GetDetailsAsync(int id, User actor);

        // For the edit form: only when the actor may edit it.
        Task<Appeal> GetForEditAsync(int id, User actor);

        // The author is the actor, the association is the author's; status New, priority Normal.
        // The appeal, its photos and the heads' notifications are saved in one transaction.
        Task<Appeal> CreateAsync(AppealInput input, IReadOnlyCollection<IFormFile> photos, User actor);

        Task UpdateAsync(int id, AppealInput input, IReadOnlyCollection<IFormFile> photos, User actor);

        // Status (AppealWorkflow), priority and comment, recorded as one AppealUpdate; the author is notified.
        Task ProcessAsync(int id, AppealProcessInput input, User actor);

        // Makes the association or a recipient responsible; the author (and the heads) are notified.
        Task AssignAsync(int id, ResponsibleKind responsible, int? recipientId, User actor);

        Task DeleteAsync(int id, User actor);

        Task<AppealStats> GetStatsAsync(User actor);

        // Open appeals of the category within the search radius of the point (the create form's warning).
        Task<IReadOnlyList<SimilarAppeal>> FindSimilarAsync(double lat, double lng, int categoryId, int? excludeId, User actor);

        // Every appeal matching the filter (no paging), for the CSV / JSON export.
        Task<IReadOnlyList<AppealExportRow>> ExportAsync(AppealFilter filter, User actor);
    }
}
