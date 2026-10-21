using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Name")]
        public string Name { get; set; } = "";

        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.NameEn")]
        public string? NameEn { get; set; }
    }
}
