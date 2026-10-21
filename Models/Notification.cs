namespace CityFix_backend.Models
{
    // StatusChanged: to the author (a status change, or a staff comment when NewStatus is null).
    // NewAppeal: to the heads of the appeal's association. Assigned: to the author and, when the
    // association becomes responsible, to its heads.
    public enum NotificationType { StatusChanged, NewAppeal, Assigned }

    // Email and SMS copies are sent by NotificationDispatcher after the change is committed.
    public enum DeliveryState { None, Pending, Sent, Failed }

    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? AppealId { get; set; }
        public Appeal? Appeal { get; set; }

        // Free text written by staff (the status-change comment) or the responsible party's (Ukrainian) name.
        public string Message { get; set; } = "";

        // Assigned: the responsible party's English name, when it has one.
        public string? MessageEn { get; set; }

        public NotificationType Type { get; set; }

        // The status the appeal moved to, so the notification text can be rendered in either UI language.
        public AppealStatus? NewStatus { get; set; }

        public bool IsRead { get; set; }

        // Shown on the Notifications page; false when the user turned in-app notifications off.
        public bool InApp { get; set; } = true;
        public DeliveryState EmailState { get; set; }
        public DeliveryState SmsState { get; set; }

        // Failed delivery attempts so far (email and SMS together).
        public int Attempts { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
