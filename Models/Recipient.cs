using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    // A city service or utility company that handles appeals. Recipients have no accounts.
    public class Recipient
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(200, ErrorMessage = "Validation.MaxLength")]
        // Ukrainian (the default UI language).
        [Display(Name = "Field.Name")]
        public string Name { get; set; } = "";

        // English. Left empty, it is filled by machine translation (TranslationWorker); the UI falls back to Name.
        [StringLength(200, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.NameEn")]
        public string? NameEn { get; set; }

        [StringLength(500, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.ContactInfo")]
        public string? ContactInfo { get; set; }
    }
}
