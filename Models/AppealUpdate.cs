namespace CityFix_backend.Models
{
    // One status change of an appeal, shown in the status timeline.
    public class AppealUpdate
    {
        public int Id { get; set; }
        public int AppealId { get; set; }
        public Appeal? Appeal { get; set; }
        public AppealStatus OldStatus { get; set; }
        public AppealStatus NewStatus { get; set; }
        public int ChangedById { get; set; }
        public User? ChangedBy { get; set; }
        public string? Comment { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
