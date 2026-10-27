using System.ComponentModel.DataAnnotations;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services;

namespace CityFix_backend.ViewModels
{
    public class ProfileViewModel
    {
        public User User { get; set; } = null!;

        public ProfileStats Stats { get; set; } = new(0, 0, 0, 0, 0);

        public ProfileFormViewModel Profile { get; set; } = new();

        public NotificationSettingsViewModel Notifications { get; set; } = new();

        public ChangePasswordViewModel ChangePassword { get; set; } = new();

        public string Initials => string.Concat(User.DisplayName().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

        public static ProfileViewModel For(User user, ProfileStats stats) => new()
        {
            User = user,
            Stats = stats,
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

        public NotificationSettings ToSettings() => new(InApp, Email);

        public static NotificationSettingsViewModel From(User user) => new()
        {
            InApp = user.NotifyInApp,
            Email = user.NotifyEmail
        };
    }
}
