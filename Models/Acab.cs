using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    // A condominium association (ОСББ). It has two invitation codes: the residents' code links a new
    // account to the association as a Resident; the head code (issued by an administrator) makes it its head.
    public class Acab
    {
        public int Id { get; set; }

        [Display(Name = "Field.Name")]
        public string Name { get; set; } = "";

        [Display(Name = "Field.Address")]
        public string? Address { get; set; }

        [Display(Name = "Field.ResidentCode")]
        public string ResidentCode { get; set; } = "";

        [Display(Name = "Field.HeadCode")]
        public string HeadCode { get; set; } = "";

        [Display(Name = "Field.CreatedAt")]
        public DateTime CreatedAt { get; set; }
        [Display(Name = "Field.UpdatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}
