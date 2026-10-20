using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.FirstName")]
        public string FirstName { get; set; } = "";

        // Required when registering (explanatory note, 1.1); accounts added by an administrator may omit it.
        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.LastName")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.Email")]
        [Display(Name = "Field.Email")]
        public string Email { get; set; } = "";

        [Phone(ErrorMessage = "Validation.Phone")]
        [Display(Name = "Field.PhoneOptional")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Validation.PasswordLength")]
        [DataType(DataType.Password)]
        [Display(Name = "Field.Password")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Validation.Required")]
        [Compare(nameof(Password), ErrorMessage = "Validation.PasswordMatch")]
        [DataType(DataType.Password)]
        [Display(Name = "Field.ConfirmPassword")]
        public string ConfirmPassword { get; set; } = "";

        // "I live in a building with an association": only then is the code asked for and used.
        [Display(Name = "Auth.Register.HasAcab")]
        public bool HasAssociation { get; set; }

        [Display(Name = "Field.AcabCode")]
        public string? InvitationCode { get; set; }
    }
}
