using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models.LC
{
    // ── Seed lookup: Issuing bodies / jurisdictions ──────────────────────────
    public class LCJurisdiction
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string JurisdictionCode { get; set; } = string.Empty;   // e.g. "TGA", "ODC", "QLD"

        [Required, MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;        // e.g. "Therapeutic Goods Administration"

        [MaxLength(500)]
        public string? IssuingBodyUrl { get; set; }

        [MaxLength(500)]
        public string? PortalUrl { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "AUD";

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    // ── Seed lookup: Licence / certification types ────────────────────────────
    public class LCLicenceType
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;               // e.g. "TGA-WDL", "ISO-9001"

        [Required, MaxLength(300)]
        public string DisplayName { get; set; } = string.Empty;

        public LCCategory Category { get; set; }

        public int DefaultRenewalLeadTimeDays { get; set; } = 90;

        public bool RequiresExpiryDate { get; set; } = true;

        public LCHolderType ApplicableHolderType { get; set; }         // OwnCompany / ThirdParty / (both uses OwnCompany as default)

        [MaxLength(200)]
        public string? IssuingBody { get; set; }

        [MaxLength(200)]
        public string? ApplicableJurisdiction { get; set; }            // comma-separated codes e.g. "QLD,NSW,SA"

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; }
    }

    // ── Conditions of licence/certification ──────────────────────────────────
    public class LCCondition
    {
        public int Id { get; set; }

        public int LicenceCertificationRecordId { get; set; }

        [ForeignKey("LicenceCertificationRecordId")]
        public virtual LicenceCertificationRecord Record { get; set; } = null!;

        [Required]
        public string ConditionDescription { get; set; } = string.Empty;

        public DateTime? DueDate { get; set; }

        public LCConditionStatus Status { get; set; } = LCConditionStatus.Active;

        public int? LinkedCAPAId { get; set; }  // FK to Capa module (no nav prop to avoid circular reference)

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? CompletedBy { get; set; }

        public DateTime? CompletedAt { get; set; }

        [MaxLength(500)]
        public string? CompletionNotes { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

    // ── Uploaded documents ────────────────────────────────────────────────────
    public class LCDocument : OpenQMS.Models.ISoftDeletedFile
    {
        public int Id { get; set; }

        public int LicenceCertificationRecordId { get; set; }

        [ForeignKey("LicenceCertificationRecordId")]
        public virtual LicenceCertificationRecord Record { get; set; } = null!;

        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string StoragePath { get; set; } = string.Empty;       // relative to WebRootPath

        public LCDocumentType DocumentType { get; set; } = LCDocumentType.Other;

        [MaxLength(500)]
        public string? Description { get; set; }

        public long FileSizeBytes { get; set; }

        [MaxLength(200)]
        public string? UploadedById { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string? FileHash { get; set; }

        public bool IsDeleted { get; set; } = false;

        public int? ApprovalCycle { get; set; }

        // Soft delete — the file and row are retained for GMP traceability; a reason is mandatory.
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        [StringLength(1000)]
        public string? DeletionReason { get; set; }
    }

    // ── Dependency link to RegIntel RegulatoryApplication ────────────────────
    public class LCDependency
    {
        public int Id { get; set; }

        public int LicenceCertificationRecordId { get; set; }

        [ForeignKey("LicenceCertificationRecordId")]
        public virtual LicenceCertificationRecord Record { get; set; } = null!;

        public int RegulatoryApplicationId { get; set; }

        [ForeignKey("RegulatoryApplicationId")]
        public virtual RI.RegulatoryApplication RegulatoryApplication { get; set; } = null!;

        public DateTime LinkedSince { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(200)]
        public string? CreatedBy { get; set; }
    }

    // ── Approval snapshot (immutable, one per approval cycle) ────────────────
    public class LCApprovalSnapshot
    {
        public int Id { get; set; }

        public int LicenceCertificationRecordId { get; set; }

        [ForeignKey("LicenceCertificationRecordId")]
        public virtual LicenceCertificationRecord Record { get; set; } = null!;

        public int RenewalCycle { get; set; }

        public DateTime TakenAt { get; set; } = DateTime.UtcNow;

        [MaxLength(256)]
        public string TakenByUserId { get; set; } = string.Empty;

        public string SnapshotJson { get; set; } = string.Empty;
    }

    // ── Immutable audit log ───────────────────────────────────────────────────
    public class LCAuditLog
    {
        public int Id { get; set; }

        public int LicenceCertificationRecordId { get; set; }

        [ForeignKey("LicenceCertificationRecordId")]
        public virtual LicenceCertificationRecord Record { get; set; } = null!;

        [Required, MaxLength(500)]
        public string Action { get; set; } = string.Empty;

        public string? Details { get; set; }

        [MaxLength(200)]
        public string? UserId { get; set; }

        [MaxLength(200)]
        public string? UserName { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;   // marks logs from subsequently-deleted records
    }
}
