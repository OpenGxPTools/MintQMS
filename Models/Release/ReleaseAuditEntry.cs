using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Release
{
    public class ReleaseAuditEntry
    {
        public int Id { get; set; }
        public int ReleaseRecordId { get; set; }

        [Required, MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Details { get; set; }

        [MaxLength(200)]
        public string? FromStatus { get; set; }

        [MaxLength(200)]
        public string? ToStatus { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? PerformedBy { get; set; }

        [MaxLength(100)]
        public string? IPAddress { get; set; }

        public ReleaseRecord? ReleaseRecord { get; set; }
    }
}
