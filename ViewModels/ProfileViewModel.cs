using CityFix_backend.Models;

namespace CityFix_backend.ViewModels
{
    public class ProfileViewModel
    {
        public User User { get; set; } = null!;

        public ChangePasswordViewModel ChangePassword { get; set; } = new();
    }
}
