using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Appeal
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Latitude")]
        [Range(-90, 90, ErrorMessage = "Validation_Range")]
        public decimal Lat { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Longitude")]
        [Range(-180, 180, ErrorMessage = "Validation_Range")]
        public decimal Lng { get; set; }

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Status")]
        public AppealStatus Status { get; set; } = AppealStatus.New;

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Priority")]
        public AppealPriority Priority { get; set; } = AppealPriority.Normal;

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Author")]
        public int AuthorId { get; set; }
        [Display(Name = "Author")]
        public User? Author { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }
        [Display(Name = "Category")]
        public Category? Category { get; set; }

        [Required(ErrorMessage = "Validation_Required")]
        [Display(Name = "Recipient")]
        public int RecipientId { get; set; }
        [Display(Name = "Recipient")]
        public Recipient? Recipient { get; set; }

        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Display(Name = "Updated")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
