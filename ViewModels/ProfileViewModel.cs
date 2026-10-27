using System.ComponentModel.DataAnnotations;
using CityFix_backend.Models;
using CityFix_backend.Services;

namespace CityFix_backend.ViewModels
{
    public class ProfileViewModel
    {
        public User User { get; set; } = null!;

        public ProfileFormViewModel Profile { get; set; } = new();

        public NotificationSettingsViewModel Notifications { get; set; } = new();

        public ChangePasswordViewModel ChangePassword { get; set; } = new();

        // Null when no bot is configured; the Telegram channel is hidden then.
        public string? TelegramBot { get; set; }

        public bool TelegramLinked => User.TelegramChatId != null;

        /// <summary>The bot's start link while a connection code is still valid.</summary>
        public string? TelegramLink(DateTime now) =>
            TelegramBot != null && User.TelegramLinkCode != null && User.TelegramLinkExpires > now
                ? $"https://t.me/{TelegramBot}?start={User.TelegramLinkCode}"
                : null;

        public static ProfileViewModel For(User user, TelegramOptions? telegram = null) => new()
        {
            User = user,
            Profile = ProfileFormViewModel.From(user),
            Notifications = NotificationSettingsViewModel.From(user),
            TelegramBot = telegram is { Enabled: true } ? telegram.BotUsername!.TrimStart('@') : null
        };
    }

    public class NotificationSettingsViewModel
    {
        [Display(Name = "Profile.Notify.InApp")]
        public bool InApp { get; set; }

        [Display(Name = "Profile.Notify.Email")]
        public bool Email { get; set; }

        [Display(Name = "Profile.Notify.Sms")]
        public bool Sms { get; set; }

        [Display(Name = "Profile.Notify.Telegram")]
        public bool Telegram { get; set; }

        public NotificationSettings ToSettings() => new(InApp, Email, Sms, Telegram);

        public static NotificationSettingsViewModel From(User user) => new()
        {
            InApp = user.NotifyInApp,
            Email = user.NotifyEmail,
            Sms = user.NotifySms,
            Telegram = user.NotifyTelegram
        };
    }
}
