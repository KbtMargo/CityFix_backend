using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.Email")]
        [Display(Name = "Field.Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.PasswordHash")]
        public string PasswordHash { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.FirstName")]
        public string FirstName { get; set; }

        [Display(Name = "Field.LastName")]
        public string? LastName { get; set; }

        [Display(Name = "Field.Phone")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.Role")]
        public UserRole Role { get; set; } = UserRole.Resident;

        [Display(Name = "Field.Acab")]
        public int? AcabId { get; set; }
        [Display(Name = "Field.Acab")]
        public Acab? Acab { get; set; }

        [Display(Name = "Field.CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Display(Name = "Field.UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
