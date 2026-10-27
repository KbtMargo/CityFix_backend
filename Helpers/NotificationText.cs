using CityFix_backend.Models;

namespace CityFix_backend.Helpers
{
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
            var lead = Lead(n);
            var comment = Comment(n);
            return comment == null ? lead : $"{lead} {SharedText.Get("Notifications.Comment")}: {comment}";
        }

        public static string Lead(Notification n)
        {
            var title = n.Appeal?.DisplayTitle() ?? "";
            if (n.Type == NotificationType.Assigned)
            {
                return SharedText.Format("Notifications.AssignedText", title, Localized.Pick(n.Message, n.MessageEn));
            }
            return n.NewStatus is AppealStatus status
                ? SharedText.Format("Notifications.StatusText", title, status.GetDisplayName())
                : SharedText.Format("Notifications.GenericText", title);
        }

        public static string? Comment(Notification n) =>
            n.Type == NotificationType.Assigned || string.IsNullOrWhiteSpace(n.Message) ? null : n.Message;

        private const int SmsTitleLength = 40;

        // Three Cyrillic (UCS-2) segments of 67 characters each.
        private const int SmsMaxLength = 201;

        public static string Sms(Notification n, string? link)
        {
            var title = Shorten(n.Appeal?.DisplayTitle() ?? "", SmsTitleLength);
            var lead = n.Type switch
            {
                NotificationType.Assigned => SharedText.Format("Sms.Assigned", title, Shorten(Localized.Pick(n.Message, n.MessageEn), SmsTitleLength)),
                NotificationType.NewAppeal => SharedText.Format("Sms.NewAppeal", title),
                _ when n.NewStatus is AppealStatus status => SharedText.Format("Sms.Status", title, status.GetDisplayName()),
                _ => SharedText.Format("Sms.Comment", title)
            };

            var text = $"{SharedText.Get("App.Name")}: {lead}";
            var tail = link == null ? "" : " " + SharedText.Format("Sms.Details", link);
            if (Comment(n) is string comment)
            {
                var prefix = $" {SharedText.Get("Notifications.Comment")}: ";
                var room = SmsMaxLength - text.Length - prefix.Length - tail.Length;
                if (room >= 15)
                {
                    text += prefix + Shorten(comment, room);
                }
            }
            return text + tail;
        }

        private static string Shorten(string text, int max)
        {
            var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return flat.Length <= max ? flat : flat[..(max - 1)].TrimEnd() + "…";
        }
    }
}
