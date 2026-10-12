using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Display(Name = "Icon")]
        public string? Icon { get; set; }
    }
}
