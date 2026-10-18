using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Acab
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.Name")]
        public string Name { get; set; }

        [Display(Name = "Field.Address")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [Display(Name = "Field.InvitationCode")]
        public string InvitationCode { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        [Display(Name = "Field.CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Display(Name = "Field.UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
