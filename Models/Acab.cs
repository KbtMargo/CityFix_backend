using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Acab
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Invitation code")]
        public string InvitationCode { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Display(Name = "Updated")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
