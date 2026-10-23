using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class AEAttachment : OpenQMS.Models.ISoftDeletedFile
    {
        public int Id { get; set; }

        [Required]
        public int AdverseEventId { get; set; }

        public AdverseEvent? AdverseEvent { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        [StringLength(256)]
        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? DocumentType { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
        public string? FileHash { get; set; }

        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }

        // Soft delete — the file and row are retained for GMP traceability; a reason is mandatory.
        [StringLength(1000)]
        public string? DeletionReason { get; set; }
    }
}
