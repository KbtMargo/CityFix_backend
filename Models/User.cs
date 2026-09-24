using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public string FirstName { get; set; }

        public string? LastName { get; set; }

        public string? Phone { get; set; }

        public UserRole Role { get; set; } = UserRole.Resident;

        public int? AcabId { get; set; }
        public Acab? Acab { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}