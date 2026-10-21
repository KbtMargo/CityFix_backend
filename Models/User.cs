using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class User
    {
        public int Id { get; set; }

        [Display(Name = "Field.Email")]
        public string Email { get; set; } = "";

        public string PasswordHash { get; set; } = "";

        public bool EmailConfirmed { get; set; }

        public string? PendingEmail { get; set; }

        public string Language { get; set; } = "uk";

        public bool NotifyInApp { get; set; } = true;
        public bool NotifyEmail { get; set; } = true;
        public bool NotifySms { get; set; }

        [Display(Name = "Field.FirstName")]
        public string FirstName { get; set; } = "";

        [Display(Name = "Field.LastName")]
        public string? LastName { get; set; }

        [Display(Name = "Field.Phone")]
        public string? Phone { get; set; }

        [Display(Name = "Field.Role")]
        public UserRole Role { get; set; } = UserRole.Resident;

        [Display(Name = "Field.Acab")]
        public int? AcabId { get; set; }
        public Acab? Acab { get; set; }

        [Display(Name = "Field.IsBlocked")]
        public bool IsBlocked { get; set; }

        [Display(Name = "Field.CreatedAt")]
        public DateTime CreatedAt { get; set; }
        [Display(Name = "Field.UpdatedAt")]
        public DateTime UpdatedAt { get; set; }

        public string? FullNameTranslated { get; set; }

        public string FullName => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";

        public bool IsStaff => Role is UserRole.Administrator or UserRole.AcabAdmin;
    }
}
