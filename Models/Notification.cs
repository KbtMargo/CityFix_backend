namespace CityFix_backend.Models
{
    public enum NotificationType { StatusChanged, NewAppeal, Assigned }

    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? AppealId { get; set; }
        public Appeal? Appeal { get; set; }

        // Free text written by staff (the status-change comment). Never translated.
        public string Message { get; set; } = null!;
        public NotificationType Type { get; set; }

        // The status the appeal moved to, so the notification text can be rendered in either UI language.
        public AppealStatus? NewStatus { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
