namespace CityFix_backend.Models
{
    // A photo attached to an appeal. The file itself lives under wwwroot/uploads/appeals/{AppealId}/.
    public class AppealFile
    {
        public int Id { get; set; }
        public int AppealId { get; set; }
        public Appeal? Appeal { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!; // relative path under wwwroot, e.g. uploads/appeals/7/3f9c….jpg
        public string ContentType { get; set; } = null!;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
