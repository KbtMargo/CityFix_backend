using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.Name")]
        public string Name { get; set; }

        [Display(Name = "Field.Icon")]
        public string? Icon { get; set; }
    }
}
