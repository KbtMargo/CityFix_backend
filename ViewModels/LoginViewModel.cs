using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.Email")]
        [Display(Name = "Field.Email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Validation.Required")]
        [DataType(DataType.Password)]
        [Display(Name = "Field.Password")]
        public string Password { get; set; } = "";

        [Display(Name = "Field.RememberMe")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
