namespace CityFix_backend.Models
{
    // One entry of an appeal's history: a status change, a priority change and/or a staff comment.
    public class AppealUpdate
    {
        public int Id { get; set; }
        public int AppealId { get; set; }
        public Appeal? Appeal { get; set; }
        public AppealStatus OldStatus { get; set; }
        public AppealStatus NewStatus { get; set; }

        // Set only when the priority changed in this update.
        public AppealPriority? OldPriority { get; set; }
        public AppealPriority? NewPriority { get; set; }

        // Set when this update made someone else responsible: the association's or recipient's name.
        public string? AssignedTo { get; set; }

        // The English name of a recipient, when it had one (AssignedTo is the Ukrainian one).
        public string? AssignedToEn { get; set; }

        public int ChangedById { get; set; }
        public User? ChangedBy { get; set; }
        public string? Comment { get; set; }
        public DateTime ChangedAt { get; set; }

        public bool StatusChanged => OldStatus != NewStatus;
        public bool PriorityChanged => NewPriority != null && OldPriority != NewPriority;
    }
}
