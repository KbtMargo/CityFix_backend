using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class FileService : IFileService
    {
        public const int MaxPhotos = 5;
        public const long MaxBytes = 10 * 1024 * 1024;

        // Extension per accepted type, plus the file signature the content must start with.
        private static readonly Dictionary<string, (string Extension, byte[][] Signatures)> Accepted = new()
        {
            ["image/png"] = (".png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } }),
            ["image/jpeg"] = (".jpg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
            ["image/gif"] = (".gif", new[] { "GIF87a"u8.ToArray(), "GIF89a"u8.ToArray() }),
            ["image/webp"] = (".webp", new[] { "RIFF"u8.ToArray() })
        };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public FileService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public void ValidatePhotos(IReadOnlyCollection<IFormFile> photos, int alreadyAttached = 0)
        {
            if (photos.Count + alreadyAttached > MaxPhotos)
            {
                throw new ServiceValidationException("Photos", "Validation.PhotosTooMany");
            }

            foreach (var photo in photos)
            {
                if (photo.Length == 0 || photo.Length > MaxBytes)
                {
                    throw new ServiceValidationException("Photos", "Validation.PhotoTooLarge");
                }
                if (DetectType(photo) == null)
                {
                    throw new ServiceValidationException("Photos", "Validation.PhotoType");
                }
            }
        }

        public async Task<List<AppealFile>> SaveAppealPhotosAsync(int appealId, IReadOnlyCollection<IFormFile> photos)
        {
            ValidatePhotos(photos, await CountAppealPhotosAsync(appealId));

            var folder = Path.Combine(_env.WebRootPath, "uploads", "appeals", appealId.ToString());
            Directory.CreateDirectory(folder);

            var saved = new List<AppealFile>();
            foreach (var photo in photos)
            {
                // Never use the client's file name or content type on disk: both come from the browser.
                var contentType = DetectType(photo)!;
                var extension = Accepted[contentType].Extension;
                var storedName = $"{Guid.NewGuid():N}{extension}";
                await using (var stream = File.Create(Path.Combine(folder, storedName)))
                {
                    await photo.CopyToAsync(stream);
                }

                saved.Add(new AppealFile
                {
                    AppealId = appealId,
                    FileName = Path.GetFileName(photo.FileName),
                    FilePath = $"uploads/appeals/{appealId}/{storedName}",
                    ContentType = contentType,
                    UploadedAt = DateTime.UtcNow
                });
            }

            _context.AppealFiles.AddRange(saved);
            await _context.SaveChangesAsync();
            return saved;
        }

        public Task<int> CountAppealPhotosAsync(int appealId) =>
            _context.AppealFiles.CountAsync(f => f.AppealId == appealId);

        public void DeleteAppealFolder(int appealId)
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads", "appeals", appealId.ToString());
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        // The real image type, from the file's first bytes; null if it isn't a PNG/JPEG/GIF/WebP.
        private static string? DetectType(IFormFile photo)
        {
            var header = new byte[12];
            using var stream = photo.OpenReadStream();
            var read = stream.Read(header, 0, header.Length);

            foreach (var (type, (_, signatures)) in Accepted)
            {
                if (signatures.Any(sig => read >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig)))
                {
                    // RIFF is shared by other formats; WebP also has "WEBP" at offset 8.
                    if (type == "image/webp" && (read < 12 || !header.AsSpan(8, 4).SequenceEqual("WEBP"u8)))
                    {
                        continue;
                    }
                    return type;
                }
            }
            return null;
        }
    }
}
