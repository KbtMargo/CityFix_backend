using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class User
    {
        public int Id { get; set; }

        // Always stored lower-cased (EmailAddress.Normalize), so lookups compare it directly.
        [Display(Name = "Field.Email")]
        public string Email { get; set; } = "";

        public string PasswordHash { get; set; } = "";

        // A self-registered account signs in only after following the link sent to Email.
        // Accounts added by an administrator (and the seeded one) start confirmed.
        public bool EmailConfirmed { get; set; }

        // A new address typed in the profile: it replaces Email once its confirmation link is followed.
        public string? PendingEmail { get; set; }

        // Language of emails and SMS ("uk" / "en"): the last one chosen with the language switch.
        public string Language { get; set; } = "uk";

        // Notification channels (Profile → Notifications). Email goes only to a confirmed address, SMS only to a phone.
        public bool NotifyInApp { get; set; } = true;
        public bool NotifyEmail { get; set; } = true;
        public bool NotifySms { get; set; }

        [Display(Name = "Field.FirstName")]
        public string FirstName { get; set; } = "";

        [Display(Name = "Field.LastName")]
        public string? LastName { get; set; }

        [Display(Name = "Field.Phone")]
        public string? Phone { get; set; }

        [Display(Name = "Field.Role")]
        public UserRole Role { get; set; } = UserRole.Resident;

        [Display(Name = "Field.Acab")]
        public int? AcabId { get; set; }
        public Acab? Acab { get; set; }

        // A blocked user cannot sign in, and an open session ends at the next request (CookieValidator).
        [Display(Name = "Field.IsBlocked")]
        public bool IsBlocked { get; set; }

        [Display(Name = "Field.CreatedAt")]
        public DateTime CreatedAt { get; set; }
        [Display(Name = "Field.UpdatedAt")]
        public DateTime UpdatedAt { get; set; }

        // The name in the other UI language: the official transliteration of a Cyrillic name, or the Ukrainian
        // spelling of a Latin one (TranslationWorker). Null until made, and again after the name changes.
        public string? FullNameTranslated { get; set; }

        public string FullName => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";

        public bool IsStaff => Role is UserRole.Administrator or UserRole.AcabAdmin;
    }
}
