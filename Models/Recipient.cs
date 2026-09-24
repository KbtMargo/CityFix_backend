using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Recipient
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? ContactInfo { get; set; }
    }
}