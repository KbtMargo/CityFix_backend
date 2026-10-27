namespace CityFix_backend.Models
{
    public class AppealFile
    {
        public int Id { get; set; }
        public int AppealId { get; set; }
        public Appeal? Appeal { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        // Small JPEG for lists and thumbnail strips; null until generated.
        public string? ThumbnailPath { get; set; }
        public string ContentType { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
    }
}
