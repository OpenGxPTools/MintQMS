using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class PSURAttachment : OpenQMS.Models.ISoftDeletedFile
    {
        public int Id { get; set; }

        public int PSURId { get; set; }

        [ForeignKey("PSURId")]
        public virtual PVPeriodicReport? PSUR { get; set; }

        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        [MaxLength(64)]
        public string? FileHash { get; set; }

        [MaxLength(256)]
        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }

        // Soft delete — the file and row are retained for GMP traceability; a reason is mandatory.
        [StringLength(1000)]
        public string? DeletionReason { get; set; }
    }
}
