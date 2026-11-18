using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace CityFix_backend.Models
{
    public enum ResponsibleKind
    {
        [Display(Name = "Enum.ResponsibleKind.Association")] Association,
        [Display(Name = "Enum.ResponsibleKind.Recipient")] Recipient
    }

    public class Appeal
    {
        public int Id { get; set; }
        [MaxLength(100)]
        public string Title { get; set; } = "";
        [MaxLength(1000)]
        public string? Description { get; set; }
        [MaxLength(2)]
        public string Language { get; set; } = "uk";
        [MaxLength(300)]
        public string? TitleTranslated { get; set; }
        [MaxLength(3000)]
        public string? DescriptionTranslated { get; set; }
        public bool TranslationPending { get; set; }
        public int TranslationAttempts { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public Point? Location { get; set; }
        [MaxLength(300)]
        public string? Address { get; set; }
        [MaxLength(600)]
        public string? AddressTranslated { get; set; }
        public AppealStatus Status { get; set; } = AppealStatus.New;
        public AppealPriority Priority { get; set; } = AppealPriority.Normal;
        public int AuthorId { get; set; }
        public User? Author { get; set; }
        public int? AcabId { get; set; }
        public Acab? Acab { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public ResponsibleKind ResponsibleKind { get; set; } = ResponsibleKind.Recipient;
        public int? RecipientId { get; set; }
        public Recipient? Recipient { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? OverdueNotifiedAt { get; set; }
        public int? Rating { get; set; }
        [MaxLength(MaxRatingCommentLength)]
        public string? RatingComment { get; set; }
        public const int MaxRatingCommentLength = 500;
        public DateTime? RatedAt { get; set; }
        public int ReopenCount { get; set; }
        public int SupportCount { get; set; }
        [Timestamp]
        public uint Version { get; set; }
        public List<AppealUpdate> Updates { get; set; } = new();
        public List<AppealFile> Files { get; set; } = new();
        public List<AppealSupport> Supports { get; set; } = new();
    }
}
