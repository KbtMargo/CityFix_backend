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

        private const int SmsTitleLength = 40;

        // Three Cyrillic (UCS-2) segments of 67 characters each.
        private const int SmsMaxLength = 201;

        public static string Sms(Notification n, string? link)
        {
            var title = Shorten(n.Appeal?.DisplayTitle() ?? "", SmsTitleLength);
            var lead = n.Type switch
            {
                _ when IsForSupporter(n) => SharedText.Format("Sms.Supported", title, n.NewStatus?.GetDisplayName() ?? ""),
                NotificationType.Assigned => SharedText.Format("Sms.Assigned", title, Shorten(Localized.Pick(n.Message, n.MessageEn), SmsTitleLength)),
                NotificationType.NewAppeal => SharedText.Format("Sms.NewAppeal", title),
                NotificationType.Reopened => SharedText.Format("Sms.Reopened", title),
                NotificationType.Rated => SharedText.Format("Sms.Rated", title, n.Appeal?.Rating ?? 0),
                NotificationType.Overdue => SharedText.Format("Sms.Overdue", title),
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

        /// <summary>Telegram has room for the whole text, so it gets the title, the full lead and comment, and the link.</summary>
        public static string Telegram(Notification n, string? link)
        {
            var text = $"{SharedText.Get("App.Name")}: {Title(n)}\n\n{Text(n)}";
            return link == null ? text : $"{text}\n\n{link}";
        }

        private static string Shorten(string text, int max)
        {
            var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return flat.Length <= max ? flat : flat[..(max - 1)].TrimEnd() + "…";
        }
    }
}
