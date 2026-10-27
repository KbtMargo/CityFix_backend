using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAppealService
    {
        Task<AppealPage> GetFilteredAsync(AppealFilter filter, User actor);

        Task<AppealDetails> GetDetailsAsync(int id, User actor);

        Task<Appeal> GetForEditAsync(int id, User actor);

        Task<Appeal> CreateAsync(AppealInput input, IReadOnlyCollection<IFormFile> photos, User actor);

        Task UpdateAsync(int id, AppealInput input, IReadOnlyCollection<IFormFile> photos, User actor);

        Task ProcessAsync(int id, AppealProcessInput input, User actor);

        Task AssignAsync(int id, ResponsibleKind responsible, int? recipientId, User actor);

        Task RateAsync(int id, AppealRatingInput input, User actor);

        Task ReopenAsync(int id, string? comment, User actor);

        Task<SupportResult> ToggleSupportAsync(int id, User actor);

        /// <summary>Tells the heads about appeals whose deadline has just passed; returns how many were found.</summary>
        Task<int> NotifyOverdueAsync();

        Task DeleteAsync(int id, User actor);

        Task<AppealFile> GetFileAsync(int fileId, User actor);

        Task<int> DeleteFileAsync(int fileId, User actor);

        Task<PagedResult<Appeal>> GetByAuthorAsync(int authorId, User actor, int page);

        Task<AppealStats> GetStatsAsync(User actor);

        Task<IReadOnlyList<SimilarAppeal>> FindSimilarAsync(double lat, double lng, int categoryId, int? excludeId, User actor);

        Task<IReadOnlyList<AppealExportRow>> ExportAsync(AppealFilter filter, User actor);
    }
}
