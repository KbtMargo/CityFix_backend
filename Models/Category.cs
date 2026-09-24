using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Category
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string? Icon { get; set; }
    }
}