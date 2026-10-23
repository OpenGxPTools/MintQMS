using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models.RI
{
    // ── Jurisdiction ──────────────────────────────────────────────────
    public class RIJurisdiction
    {
        public int Id { get; set; }

        [Required, MaxLength(10)]
        public string Code { get; set; } = string.Empty;   // AU, NZ, EU, US, CA, SG, MY, BR, MX, CO

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;   // Australia

        [Required, MaxLength(150)]
        public string Authority { get; set; } = string.Empty;  // TGA

        [MaxLength(200)]
        public string? AuthorityUrl { get; set; }

        [MaxLength(200)]
        public string? PortalUrl { get; set; }

        [MaxLength(10)]
        public string? CurrencyCode { get; set; }

        public bool IsActive  { get; set; } = true;
        public int  SortOrder { get; set; }

        public virtual ICollection<RIApplicationType> ApplicationTypes { get; set; } = new List<RIApplicationType>();
    }

    // ── Application Type ─────────────────────────────────────────────
    public class RIApplicationType
    {
        public int Id { get; set; }

        public int JurisdictionId { get; set; }

        [ForeignKey("JurisdictionId")]
        public virtual RIJurisdiction Jurisdiction { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;   // Cat1, SAS-B, NDA-505b2

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public RIApplicationCategory Category { get; set; }

        public bool RequiresApproval { get; set; } = true;  // false = notification-only (SAS-A, SAS-C)

        public int? ClockDays { get; set; }  // regulatory review clock in working days

        public bool IsActive  { get; set; } = true;
        public int  SortOrder { get; set; }
    }

    // ── Milestone ────────────────────────────────────────────────────
    public class RIMilestone
    {
        public int Id { get; set; }

        public int RegulatoryApplicationId { get; set; }

        [ForeignKey("RegulatoryApplicationId")]
        public virtual RegulatoryApplication Application { get; set; } = null!;

        [Required, MaxLength(200)]
        public string MilestoneName { get; set; } = string.Empty;

        public DateTime? PlannedDate  { get; set; }
        public DateTime? ActualDate   { get; set; }

        public RIMilestoneStatus Status { get; set; } = RIMilestoneStatus.Pending;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool      IsClockStop       { get; set; }
        public DateTime? ClockRestartDate  { get; set; }

        [Required, MaxLength(256)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Condition of Approval ─────────────────────────────────────────
    public class RIConditionOfApproval
    {
        public int Id { get; set; }

        public int RegulatoryApplicationId { get; set; }

        [ForeignKey("RegulatoryApplicationId")]
        public virtual RegulatoryApplication Application { get; set; } = null!;

        [Required, MaxLength(500)]
        public string ConditionDescription { get; set; } = string.Empty;

        public RIConditionType   ConditionType { get; set; }
        public DateTime?         DueDate       { get; set; }
        public RIConditionStatus Status        { get; set; } = RIConditionStatus.Outstanding;

        [MaxLength(500)]
        public string? CompletionNotes { get; set; }

        public DateTime? CompletedDate { get; set; }

        public int? LinkedCAPAId { get; set; }  // FK to CAPA module

        [Required, MaxLength(256)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Variation ────────────────────────────────────────────────────
    public class RIVariation
    {
        public int Id { get; set; }

        public int ParentApplicationId { get; set; }

        [ForeignKey("ParentApplicationId")]
        public virtual RegulatoryApplication ParentApplication { get; set; } = null!;

        [Required, MaxLength(30)]
        public string VariationNumber { get; set; } = string.Empty;   // RI-2026-0001-V1

        [Required, MaxLength(300)]
        public string VariationDescription { get; set; } = string.Empty;

        public RIVariationType     VariationType { get; set; }
        public RIApplicationStatus Status        { get; set; } = RIApplicationStatus.Strategy;

        public DateTime? SubmissionDate { get; set; }
        public DateTime? ApprovalDate   { get; set; }
        public string?   Notes          { get; set; }

        [Required, MaxLength(256)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Access Pathway Record ─────────────────────────────────────────
    public class RIAccessPathwayRecord
    {
        public int Id { get; set; }

        public int RegulatoryApplicationId { get; set; }

        [ForeignKey("RegulatoryApplicationId")]
        public virtual RegulatoryApplication Application { get; set; } = null!;

        [Required, MaxLength(25)]
        public string AccessNumber { get; set; } = string.Empty;   // AP-YYYY-NNNN

        public RIAccessPathwayType PathwayType { get; set; }

        [MaxLength(50)]
        public string? PatientIdentifier { get; set; }   // pseudonymised — initials or code

        [MaxLength(20)]
        public string? PrescriberId { get; set; }        // AHPRA# or equivalent

        [MaxLength(200)]
        public string? PrescriberName { get; set; }

        [MaxLength(200)]
        public string? TGAApprovalLetterNumber { get; set; }

        public DateTime? ApprovalDate { get; set; }
        public DateTime? ExpiryDate   { get; set; }

        public RIAccessPathwayStatus Status { get; set; } = RIAccessPathwayStatus.Requested;

        public DateTime? NextSixMonthlyReportDue { get; set; }

        public bool AEReportingRequired { get; set; } = true;

        [MaxLength(1000)]
        public string? ClinicalJustification { get; set; }

        [Required, MaxLength(256)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<RISixMonthlySupplyReport> SupplyReports { get; set; } = new List<RISixMonthlySupplyReport>();
    }

    // ── Six-Monthly Supply Report ─────────────────────────────────────
    public class RISixMonthlySupplyReport
    {
        public int Id { get; set; }

        public int AccessPathwayRecordId { get; set; }

        [ForeignKey("AccessPathwayRecordId")]
        public virtual RIAccessPathwayRecord AccessPathway { get; set; } = null!;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd   { get; set; }
        public int      QuantitySupplied { get; set; }

        [MaxLength(50)]
        public string? Unit { get; set; }   // vials, tablets, packs

        public DateTime? SubmittedToAuthorityDate { get; set; }

        [MaxLength(100)]
        public string? AuthorityReferenceNumber { get; set; }

        public RIReportStatus Status { get; set; } = RIReportStatus.Draft;

        public string? Notes { get; set; }

        [Required, MaxLength(256)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Document ─────────────────────────────────────────────────────
    public class RIDocument : OpenQMS.Models.ISoftDeletedFile
    {
        public int Id { get; set; }

        public int RegulatoryApplicationId { get; set; }

        [ForeignKey("RegulatoryApplicationId")]
        public virtual RegulatoryApplication Application { get; set; } = null!;

        [Required, MaxLength(200)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        // DocumentType: eCTD, ApprovalLetter, PI, CMI, SmPC, REMS, RMP, ConditionLetter, AssessmentReport, Labelling
        [MaxLength(100)]
        public string? DocumentType { get; set; }

        public long FileSizeBytes { get; set; }
        public int  Version       { get; set; } = 1;

        [Required, MaxLength(256)]
        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string? FileHash { get; set; }

        public int? ApprovalCycle { get; set; }

        // Soft delete — the file and row are retained for GMP traceability; a reason is mandatory.
        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        [StringLength(1000)]
        public string? DeletionReason { get; set; }
    }

    // ── Approval Snapshot (immutable, one per approval cycle) ────────────────
    public class RIApprovalSnapshot
    {
        public int Id { get; set; }

        public int RegulatoryApplicationId { get; set; }

        [ForeignKey("RegulatoryApplicationId")]
        public virtual RegulatoryApplication Application { get; set; } = null!;

        public int RenewalCycle { get; set; }

        public DateTime TakenAt { get; set; } = DateTime.UtcNow;

        [MaxLength(256)]
        public string TakenByUserId { get; set; } = string.Empty;

        public string SnapshotJson { get; set; } = string.Empty;
    }

    // ── Audit Log ────────────────────────────────────────────────────
    public class RIAuditLog
    {
        public int Id { get; set; }

        public int RegulatoryApplicationId { get; set; }

        [Required, MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? FieldChanged { get; set; }

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        [Required, MaxLength(256)]
        public string UserId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
