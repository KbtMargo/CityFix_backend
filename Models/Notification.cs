namespace CityFix_backend.Models
{
    public enum NotificationType { StatusChanged, NewAppeal, Assigned }

    public enum DeliveryState { None, Pending, Sent, Failed }

    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? AppealId { get; set; }
        public Appeal? Appeal { get; set; }

        public string Message { get; set; } = "";

        public string? MessageEn { get; set; }

        public NotificationType Type { get; set; }

        public AppealStatus? NewStatus { get; set; }

        public bool IsRead { get; set; }

        public bool InApp { get; set; } = true;
        public DeliveryState EmailState { get; set; }
        public DeliveryState SmsState { get; set; }

        public int Attempts { get; set; }

        // Pending e-mail/SMS copies are picked up once this moment passes; also leases a row to one dispatcher.
        public DateTime NextAttemptAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
