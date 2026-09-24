using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class Appeal
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string? Description { get; set; }

        [Range(-90, 90)]
        public decimal Lat { get; set; }

        [Range(-180, 180)]
        public decimal Lng { get; set; }

        public string? Address { get; set; }

        public AppealStatus Status { get; set; } = AppealStatus.New;
        public AppealPriority Priority { get; set; } = AppealPriority.Normal;

        public int AuthorId { get; set; }
        public User? Author { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int RecipientId { get; set; }
        public Recipient? Recipient { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}