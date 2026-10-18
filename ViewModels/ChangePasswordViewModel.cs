using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [DataType(DataType.Password)]
        [Display(Name = "Field.CurrentPassword")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Validation.PasswordLength")]
        [DataType(DataType.Password)]
        [Display(Name = "Field.NewPassword")]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Validation.Required")]
        [Compare(nameof(NewPassword), ErrorMessage = "Validation.PasswordMatch")]
        [DataType(DataType.Password)]
        [Display(Name = "Field.ConfirmNewPassword")]
        public string ConfirmPassword { get; set; } = "";
    }
}
