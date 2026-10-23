using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AssetDocument : OpenQMS.Models.ISoftDeletedFile
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }

        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        [Display(Name = "Content Type")]
        public string? ContentType { get; set; }

        [Display(Name = "File Size")]
        public long FileSize { get; set; }

        [Display(Name = "Issue Date")]
        public DateTime? IssueDate { get; set; }

        [Display(Name = "Expiry Date")]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "Reminder Days")]
        public int ReminderDays { get; set; } = 30;

        [Display(Name = "Document Status")]
        public AssetDocumentStatus DocumentStatus { get; set; } = AssetDocumentStatus.Valid;

        [Display(Name = "Description")]
        public string? Description { get; set; }
        public string? FileHash { get; set; }

        [Display(Name = "Uploaded By")]
        public string? UploadedBy { get; set; }

        [Display(Name = "Uploaded Date")]
        public DateTime UploadedDate { get; set; }

        // Soft delete — the file and row are retained for GMP traceability; a reason is mandatory.
        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        [StringLength(1000)]
        public string? DeletionReason { get; set; }
    }

    public enum AssetDocumentStatus
    {
        Valid,
        Expiring,
        Expired
    }
}
