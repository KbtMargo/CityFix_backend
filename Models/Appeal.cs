using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace CityFix_backend.Models
{
    // Who handles an appeal: the appeal's own association, or one of the recipients (city services).
    public enum ResponsibleKind
    {
        [Display(Name = "Enum.ResponsibleKind.Association")] Association,
        [Display(Name = "Enum.ResponsibleKind.Recipient")] Recipient
    }

    // A citizen's appeal. The class only holds data: the rules (who may see, edit or process it, which
    // status transitions are allowed) live in the service layer (AppealService, AppealPolicy, AppealWorkflow).
    public class Appeal
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Title { get; set; } = "";

        [MaxLength(1000)]
        public string? Description { get; set; }

        // Title and description as the author wrote them are in Language ("uk" / "en"); the Translated fields hold
        // the machine translation into the other UI language, made in the background (TranslationWorker) after
        // every change of the text. Until it is ready (or if it fails) everyone sees the original.
        [MaxLength(2)]
        public string Language { get; set; } = "uk";

        [MaxLength(300)]
        public string? TitleTranslated { get; set; }

        [MaxLength(3000)]
        public string? DescriptionTranslated { get; set; }

        public bool TranslationPending { get; set; }
        public int TranslationAttempts { get; set; }

        // Entered on the map as two numbers (invariant culture, decimal dot).
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }

        // geography(Point, 4326), computed by PostgreSQL from Lat/Lng and covered by a GiST index:
        // the 500 m similar-appeals search runs on it (ST_DWithin). Never written by the app.
        public Point? Location { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        // The address in the other UI language (TranslationWorker); null until made and after the address changes.
        [MaxLength(600)]
        public string? AddressTranslated { get; set; }

        public AppealStatus Status { get; set; } = AppealStatus.New;
        public AppealPriority Priority { get; set; } = AppealPriority.Normal;

        public int AuthorId { get; set; }
        public User? Author { get; set; }

        // The author's association when the appeal was created; it stays with the appeal if the author moves.
        public int? AcabId { get; set; }
        public Acab? Acab { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        // Responsible party: the association (RecipientId is null) or a recipient.
        public ResponsibleKind ResponsibleKind { get; set; } = ResponsibleKind.Recipient;
        public int? RecipientId { get; set; }
        public Recipient? Recipient { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // PostgreSQL's xmin: two staff members saving the same appeal at once get a concurrency error.
        [Timestamp]
        public uint Version { get; set; }

        public List<AppealUpdate> Updates { get; set; } = new();
        public List<AppealFile> Files { get; set; } = new();
    }
}
