using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IFileService
    {
        // Throws ServiceValidationException (field "Photos") when there are too many files,
        // a file is too large, or it isn't a PNG/JPEG/GIF/WebP image. Call before saving anything.
        void ValidatePhotos(IReadOnlyCollection<IFormFile> photos, int alreadyAttached = 0);

        // Writes the photos under wwwroot/uploads/appeals/{appealId}/ and records AppealFile rows.
        Task<List<AppealFile>> SaveAppealPhotosAsync(int appealId, IReadOnlyCollection<IFormFile> photos);

        Task<int> CountAppealPhotosAsync(int appealId);

        // Removes the appeal's photo folder (the rows go with the appeal through the FK cascade).
        void DeleteAppealFolder(int appealId);
    }
}
