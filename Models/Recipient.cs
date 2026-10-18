using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Recipient
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.Name")]
        public string Name { get; set; }

        [Display(Name = "Field.ContactInfo")]
        public string? ContactInfo { get; set; }
    }
}
