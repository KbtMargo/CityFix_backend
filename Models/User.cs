using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [EmailAddress(ErrorMessage = "Validation_Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Password hash")]
        public string PasswordHash { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Display(Name = "Last name")]
        public string? LastName { get; set; }

        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Role")]
        public UserRole Role { get; set; } = UserRole.Resident;

        [Display(Name = "Acab")]
        public int? AcabId { get; set; }
        [Display(Name = "Acab")]
        public Acab? Acab { get; set; }

        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Display(Name = "Updated")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
