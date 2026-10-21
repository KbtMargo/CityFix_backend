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

        public static ProfileViewModel For(User user) => new()
        {
            User = user,
            Profile = ProfileFormViewModel.From(user),
            Notifications = NotificationSettingsViewModel.From(user)
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

        public NotificationSettings ToSettings() => new(InApp, Email, Sms);

        public static NotificationSettingsViewModel From(User user) => new()
        {
            InApp = user.NotifyInApp,
            Email = user.NotifyEmail,
            Sms = user.NotifySms
        };
    }
}
