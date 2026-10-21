using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Services
{
    public class FileService : IFileService
    {
        private static readonly Dictionary<string, (string Extension, byte[][] Signatures)> Accepted = new()
        {
            ["image/png"] = (".png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } }),
            ["image/jpeg"] = (".jpg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
            ["image/gif"] = (".gif", new[] { "GIF87a"u8.ToArray(), "GIF89a"u8.ToArray() }),
            ["image/webp"] = (".webp", new[] { "RIFF"u8.ToArray() })
        };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UploadOptions _options;
        private readonly TimeProvider _clock;

        public FileService(ApplicationDbContext context, IWebHostEnvironment env, IOptions<UploadOptions> options, TimeProvider clock)
        {
            _context = context;
            _env = env;
            _options = options.Value;
            _clock = clock;
        }

        public int MaxPhotos => _options.MaxPhotos;

        public void ValidatePhotos(IReadOnlyCollection<IFormFile> photos, int alreadyAttached = 0)
        {
            if (photos.Count + alreadyAttached > _options.MaxPhotos)
            {
                throw new ServiceValidationException("Photos", "Validation.PhotosTooMany", _options.MaxPhotos);
            }

            foreach (var photo in photos)
            {
                if (photo.Length == 0 || photo.Length > _options.MaxBytes)
                {
                    throw new ServiceValidationException("Photos", "Validation.PhotoTooLarge", _options.MaxBytes / (1024 * 1024));
                }
                if (DetectType(photo) == null)
                {
                    throw new ServiceValidationException("Photos", "Validation.PhotoType");
                }
            }
        }

        public async Task<IReadOnlyList<string>> WriteAppealPhotosAsync(Appeal appeal, IReadOnlyCollection<IFormFile> photos)
        {
            var written = new List<string>();
            if (photos.Count == 0) return written;

            var folder = FolderOf(appeal.Id);
            Directory.CreateDirectory(folder);

            foreach (var photo in photos)
            {
                var contentType = DetectType(photo)!;
                var storedName = $"{Guid.NewGuid():N}{Accepted[contentType].Extension}";
                var fullPath = Path.Combine(folder, storedName);
                await using (var stream = File.Create(fullPath))
                {
                    await photo.CopyToAsync(stream);
                }
                written.Add(fullPath);

                _context.AppealFiles.Add(new AppealFile
                {
                    AppealId = appeal.Id,
                    FileName = Path.GetFileName(photo.FileName),
                    FilePath = $"uploads/appeals/{appeal.Id}/{storedName}",
                    ContentType = contentType,
                    UploadedAt = _clock.GetUtcNow().UtcDateTime
                });
            }
            return written;
        }

        public void DeleteFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        public void DeleteAppealFolder(int appealId)
        {
            var folder = FolderOf(appealId);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private string FolderOf(int appealId) => Path.Combine(_env.WebRootPath, "uploads", "appeals", appealId.ToString());

        private static string? DetectType(IFormFile photo)
        {
            var header = new byte[12];
            using var stream = photo.OpenReadStream();
            var read = stream.Read(header, 0, header.Length);

            foreach (var (type, (_, signatures)) in Accepted)
            {
                if (signatures.Any(sig => read >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig)))
                {
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
