namespace CityFix_backend.Models
{
    public class AppealUpdate
    {
        public int Id { get; set; }
        public int AppealId { get; set; }
        public Appeal? Appeal { get; set; }
        public AppealStatus OldStatus { get; set; }
        public AppealStatus NewStatus { get; set; }
        public AppealPriority? OldPriority { get; set; }
        public AppealPriority? NewPriority { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToEn { get; set; }
        public int ChangedById { get; set; }
        public User? ChangedBy { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(MaxCommentLength)]
        public string? Comment { get; set; }
        public const int MaxCommentLength = 1000;
        public DateTime ChangedAt { get; set; }
        public int? Rating { get; set; }
        public bool StatusChanged => OldStatus != NewStatus;
        public bool PriorityChanged => NewPriority != null && OldPriority != NewPriority;
    }
}
