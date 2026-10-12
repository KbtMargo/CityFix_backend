using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Recipient
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Display(Name = "Contact info")]
        public string? ContactInfo { get; set; }
    }
}
