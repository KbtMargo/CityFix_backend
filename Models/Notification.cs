namespace CityFix_backend.Models
{
    // StatusChanged: to the author (a status change, or a staff comment when NewStatus is null).
    // NewAppeal: to the heads of the appeal's association. Assigned: to the author and, when the
    // association becomes responsible, to its heads.
    public enum NotificationType { StatusChanged, NewAppeal, Assigned }

    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? AppealId { get; set; }
        public Appeal? Appeal { get; set; }

        // Free text written by staff (the status-change comment) or the responsible party's name. Never translated.
        public string Message { get; set; } = "";
        public NotificationType Type { get; set; }

        // The status the appeal moved to, so the notification text can be rendered in either UI language.
        public AppealStatus? NewStatus { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
