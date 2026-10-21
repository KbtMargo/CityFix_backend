using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        // Ukrainian (the default UI language).
        [Display(Name = "Field.Name")]
        public string Name { get; set; } = "";

        // English. Left empty, it is filled by machine translation (TranslationWorker); the UI falls back to Name.
        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.NameEn")]
        public string? NameEn { get; set; }
    }
}
