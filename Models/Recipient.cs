using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Recipient
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(200, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Name")]
        public string Name { get; set; } = "";

        [StringLength(200, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.NameEn")]
        public string? NameEn { get; set; }

        [StringLength(500, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.ContactInfo")]
        public string? ContactInfo { get; set; }
    }
}
