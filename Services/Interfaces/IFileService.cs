using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IFileService
    {
        int MaxPhotos { get; }

        // Throws ServiceValidationException (field "Photos") when there are too many files,
        // a file is too large, or it isn't a PNG/JPEG/GIF/WebP image. Call before saving anything.
        void ValidatePhotos(IReadOnlyCollection<IFormFile> photos, int alreadyAttached = 0);

        // Writes the photos under wwwroot/uploads/appeals/{appealId}/ and adds AppealFile rows to the
        // unit of work (the caller saves). Returns the written paths so a failed save can remove them.
        Task<IReadOnlyList<string>> WriteAppealPhotosAsync(Appeal appeal, IReadOnlyCollection<IFormFile> photos);

        void DeleteFiles(IEnumerable<string> paths);

        // Removes the appeal's photo folder (the rows go with the appeal through the FK cascade).
        void DeleteAppealFolder(int appealId);
    }
}
