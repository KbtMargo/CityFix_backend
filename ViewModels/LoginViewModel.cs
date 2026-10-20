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

        // Set by the controller: the password was right but the email is not confirmed yet (offer a new letter).
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public bool NeedsConfirmation { get; set; }
    }

    // "Send the confirmation letter again" (and the CheckEmail page, which shows the address).
    public class ResendConfirmationViewModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.Email")]
        [Display(Name = "Field.Email")]
        public string Email { get; set; } = "";
    }
}
