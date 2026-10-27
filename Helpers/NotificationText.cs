using CityFix_backend.Models;

namespace CityFix_backend.Helpers
{
    public static class NotificationText
    {
        /// <summary>
        /// A status change sent to someone other than the author went to a resident who pressed "affects me too".
        /// The staff comment is addressed to the author, so it stays out of their text.
        /// </summary>
        public static bool IsForSupporter(Notification n) =>
            n.Type == NotificationType.StatusChanged && n.Appeal != null && n.Appeal.AuthorId != n.UserId;

        public static string Title(Notification n) => n.Type switch
        {
            _ when IsForSupporter(n) => SharedText.Get("Notifications.SupportedTitle"),
            NotificationType.StatusChanged when n.NewStatus == null => SharedText.Get("Notifications.CommentTitle"),
            NotificationType.StatusChanged => SharedText.Get("Notifications.StatusTitle"),
            NotificationType.NewAppeal => SharedText.Get("Notifications.NewAppealTitle"),
            NotificationType.Reopened => SharedText.Get("Notifications.ReopenedTitle"),
            NotificationType.Rated => SharedText.Get("Notifications.RatedTitle"),
            NotificationType.Overdue => SharedText.Get("Notifications.OverdueTitle"),
            _ => SharedText.Get("Notifications.AssignedTitle")
        };

        public static string Text(Notification n)
        {
            var lead = Lead(n);
            var comment = Comment(n);
            return comment == null ? lead : $"{lead} {SharedText.Get("Notifications.Comment")}: {comment}";
        }

        public static string Lead(Notification n)
        {
            var title = n.Appeal?.DisplayTitle() ?? "";
            if (IsForSupporter(n))
            {
                return SharedText.Format("Notifications.SupportedText", title, n.NewStatus?.GetDisplayName() ?? "");
            }
            return n.Type switch
            {
                NotificationType.Assigned => SharedText.Format("Notifications.AssignedText", title, Localized.Pick(n.Message, n.MessageEn)),
                NotificationType.Reopened => SharedText.Format("Notifications.ReopenedText", title),
                NotificationType.Rated => SharedText.Format("Notifications.RatedText", title, n.Appeal?.Rating ?? 0),
                NotificationType.Overdue => SharedText.Format("Notifications.OverdueText", title),
                _ when n.NewStatus is AppealStatus status => SharedText.Format("Notifications.StatusText", title, status.GetDisplayName()),
                _ => SharedText.Format("Notifications.GenericText", title)
            };
        }

        public static string? Comment(Notification n) =>
            n.Type == NotificationType.Assigned || IsForSupporter(n) || string.IsNullOrWhiteSpace(n.Message) ? null : n.Message;
    }
}
