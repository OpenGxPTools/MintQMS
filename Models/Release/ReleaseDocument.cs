using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Release
{
    public class ReleaseDocument : OpenQMS.Models.ISoftDeletedFile
    {
        public int Id { get; set; }
        public int ReleaseRecordId { get; set; }

        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? FilePath { get; set; }

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public long FileSizeBytes { get; set; }

        [MaxLength(200)]
        [Display(Name = "Document Category")]
        public string? DocumentCategory { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? UploadedBy { get; set; }

        [MaxLength(64)]
        public string? FileHash { get; set; }

        public ReleaseRecord? ReleaseRecord { get; set; }

        // Soft delete — the file and row are retained for GMP traceability; a reason is mandatory.
        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        [StringLength(1000)]
        public string? DeletionReason { get; set; }
    }
}
