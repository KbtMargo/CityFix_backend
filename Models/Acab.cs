using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Acab
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? Address { get; set; }

        [Required]
        public string InvitationCode { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}