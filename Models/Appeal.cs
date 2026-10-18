using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Appeal
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, MinimumLength = 5, ErrorMessage = "Validation.Length")]
        [Display(Name = "Field.Title")]
        public string Title { get; set; } = "";

        [StringLength(1000, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Description")]
        public string? Description { get; set; }

        // Optional: set by the map picker. Without a point the address is required (checked in AppealService).
        [Display(Name = "Field.Latitude")]
        [Range(-90, 90, ErrorMessage = "Validation.Range")]
        public decimal? Lat { get; set; }

        [Display(Name = "Field.Longitude")]
        [Range(-180, 180, ErrorMessage = "Validation.Range")]
        public decimal? Lng { get; set; }

        [StringLength(300, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Address")]
        public string? Address { get; set; }

        [Display(Name = "Field.Status")]
        public AppealStatus Status { get; set; } = AppealStatus.New;

        [Display(Name = "Field.Priority")]
        public AppealPriority Priority { get; set; } = AppealPriority.Normal;

        [Display(Name = "Field.Author")]
        public int AuthorId { get; set; }
        public User? Author { get; set; }

        [Required(ErrorMessage = "Validation.Choose")]
        [Display(Name = "Field.Category")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        // Nullable in the database (a deleted recipient leaves its appeals unassigned), but required on the form.
        [Required(ErrorMessage = "Validation.Choose")]
        [Display(Name = "Field.Recipient")]
        public int? RecipientId { get; set; }
        public Recipient? Recipient { get; set; }

        [Display(Name = "Field.CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Display(Name = "Field.UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<AppealUpdate> Updates { get; set; } = new();
        public List<AppealFile> Files { get; set; } = new();
    }
}
