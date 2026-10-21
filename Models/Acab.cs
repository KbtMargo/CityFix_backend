using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Acab
    {
        public int Id { get; set; }

        [Display(Name = "Field.Name")]
        public string Name { get; set; } = "";

        [Display(Name = "Field.NameEn")]
        public string? NameEn { get; set; }

        [Display(Name = "Field.Address")]
        public string? Address { get; set; }

        [Display(Name = "Field.AddressEn")]
        public string? AddressEn { get; set; }

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
