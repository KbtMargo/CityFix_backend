using CityFix_backend.Models;

namespace CityFix_backend.Helpers
{
    // The text of a notification in the current UI language: the Notifications page and the email / SMS copies
    // (NotificationDispatcher switches the culture to the recipient's language first). Needs Appeal loaded.
    public static class NotificationText
    {
        public static string Title(Notification n) => n.Type switch
        {
            NotificationType.StatusChanged when n.NewStatus == null => SharedText.Get("Notifications.CommentTitle"),
            NotificationType.StatusChanged => SharedText.Get("Notifications.StatusTitle"),
            NotificationType.NewAppeal => SharedText.Get("Notifications.NewAppealTitle"),
            _ => SharedText.Get("Notifications.AssignedTitle")
        };

        public static string Text(Notification n)
        {
            var title = n.Appeal?.DisplayTitle() ?? "";
            if (n.Type == NotificationType.Assigned)
            {
                // Message / MessageEn hold the responsible party's name.
                return SharedText.Format("Notifications.AssignedText", title, Localized.Pick(n.Message, n.MessageEn));
            }
            var text = n.NewStatus is AppealStatus status
                ? SharedText.Format("Notifications.StatusText", title, status.GetDisplayName())
                : SharedText.Format("Notifications.GenericText", title);
            return string.IsNullOrWhiteSpace(n.Message) ? text : $"{text} {SharedText.Get("Notifications.Comment")}: {n.Message}";
        }
    }
}
