using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public class AuditEntry
    {
        public long Id { get; set; }
        public DateTime At { get; set; }
        public int? ActorId { get; set; }
        [MaxLength(200)]
        public string ActorName { get; set; } = "";
        [MaxLength(64)]
        public string Action { get; set; } = "";
        [MaxLength(200)]
        public string Target { get; set; } = "";
        [MaxLength(1000)]
        public string? Details { get; set; }
        [MaxLength(64)]
        public string? IpAddress { get; set; }
    }
}
