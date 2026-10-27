using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IFileService
    {
        int MaxPhotos { get; }

        void ValidatePhotos(IReadOnlyCollection<IFormFile> photos, int alreadyAttached = 0);

        Task<IReadOnlyList<string>> WriteAppealPhotosAsync(Appeal appeal, IReadOnlyCollection<IFormFile> photos);

        void DeleteFiles(IEnumerable<string> paths);

        void DeleteAppealFolder(int appealId);

        string PhysicalPathOf(AppealFile file);

        string? ThumbnailPathOf(AppealFile file);

        /// <summary>The photo and its thumbnail, for deletion.</summary>
        IReadOnlyList<string> PhysicalPathsOf(AppealFile file);

        /// <summary>Generates thumbnails for photos uploaded before they existed; returns how many were made.</summary>
        Task<int> CreateMissingThumbnailsAsync(CancellationToken cancellationToken = default);
    }
}
