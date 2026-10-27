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
    }
}
