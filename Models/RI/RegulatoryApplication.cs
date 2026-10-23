using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models.RI
{
    public class RegulatoryApplication
    {
        public int Id { get; set; }

        // Auto-generated reference number
        [Required, MaxLength(20)]
        public string RINumber { get; set; } = string.Empty;  // RI-YYYY-NNNN

        // Product & jurisdiction links
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;

        public int JurisdictionId { get; set; }

        [ForeignKey("JurisdictionId")]
        public virtual RIJurisdiction Jurisdiction { get; set; } = null!;

        public int ApplicationTypeId { get; set; }

        [ForeignKey("ApplicationTypeId")]
        public virtual RIApplicationType ApplicationType { get; set; } = null!;

        // Core descriptors (denormalised snapshot at creation)
        [Required, MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ActiveSubstance { get; set; }

        [MaxLength(200)]
        public string? Indication { get; set; }

        [MaxLength(200)]
        public string? DosageForm { get; set; }

        [MaxLength(100)]
        public string? Strength { get; set; }

        // Authority reference
        [MaxLength(100)]
        public string? AuthorityReferenceNumber { get; set; }  // ARTG#, NDA#, EMA/H/...

        [MaxLength(50)]
        public string? SubmissionMethod { get; set; }           // eCTD, PRISM, Portal, Paper

        // Status & workflow
        public RIApplicationStatus Status    { get; set; } = RIApplicationStatus.Strategy;
        public bool                IsLocked  { get; set; } = false;

        // Priority & expedited flags
        public bool IsOrphanDesignation   { get; set; }
        public bool IsPriorityReview      { get; set; }
        public bool IsBreakthroughTherapy { get; set; }
        public bool IsPRIME               { get; set; }
        public bool IsConditionalApproval { get; set; }

        // Key dates
        public DateTime? StrategyStartDate    { get; set; }
        public DateTime? PreSubMeetingDate    { get; set; }
        public DateTime? DossierLockDate      { get; set; }
        public DateTime? SubmissionDate       { get; set; }
        public DateTime? AcknowledgementDate  { get; set; }
        public DateTime? Day0Date             { get; set; }   // regulatory clock start
        public DateTime? TargetDecisionDate   { get; set; }   // auto: Day0 + ClockDays
        public DateTime? ActualDecisionDate   { get; set; }
        public DateTime? ApprovalDate         { get; set; }
        public DateTime? ExpiryDate           { get; set; }
        public DateTime? NextRenewalDue       { get; set; }
        public DateTime? AppealDeadline       { get; set; }

        // Fee tracking
        public decimal?  ApplicationFee    { get; set; }

        [MaxLength(10)]
        public string? FeeCurrency         { get; set; }

        public DateTime? FeePaidDate       { get; set; }
        public bool      FeeWaiverApplied  { get; set; }

        [MaxLength(200)]
        public string? FeeWaiverReason     { get; set; }

        // Contacts
        [MaxLength(150)]
        public string? InternalRALead        { get; set; }

        [MaxLength(150)]
        public string? ExternalConsultant    { get; set; }

        [MaxLength(150)]
        public string? AuthorityRapporteur   { get; set; }

        [MaxLength(250)]
        public string? AuthorityContactEmail { get; set; }

        public int? ManufacturingSupplierId  { get; set; }

        // Notes
        public string? StrategyNotes    { get; set; }
        public string? RejectionReason  { get; set; }
        public string? WithdrawalReason { get; set; }
        public string? SuspensionReason { get; set; }

        // Hierarchy — variations & renewals link to parent
        public int? ParentApplicationId { get; set; }

        [ForeignKey("ParentApplicationId")]
        public virtual RegulatoryApplication? ParentApplication { get; set; }

        // Audit
        [Required, MaxLength(256)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;

        [MaxLength(256)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }
        public bool      IsDeleted  { get; set; } = false;
        public string?   DeletedBy  { get; set; }
        public DateTime? DeletedOn  { get; set; }
        public DateTime? WithdrawnOn { get; set; }
        public DateTime? SuspendedOn { get; set; }

        // ── Gap 13: Retention & Disposal ─────────────────────────────────────
        public DateTime?          RetainUntil                   { get; set; }
        public bool               RetentionExtended             { get; set; } = false;
        public RetentionHoldReason? RetentionExtendedReason     { get; set; }
        public string?            RetentionExtendedComments     { get; set; }
        public string?            RetentionExtendedBy           { get; set; }
        public DateTime?          RetentionExtendedOn           { get; set; }
        public string?            RetentionExtendedRemovalReason { get; set; }
        public string?            RetentionExtendedRemovedBy    { get; set; }
        public DateTime?          RetentionExtendedRemovedOn    { get; set; }
        public DateTime?          RetentionHoldCooldownUntil    { get; set; }

        public int RenewalCycle { get; set; } = 0;

        // Navigation collections
        public virtual ICollection<RIMilestone>           Milestones        { get; set; } = new List<RIMilestone>();
        public virtual ICollection<RIConditionOfApproval> Conditions        { get; set; } = new List<RIConditionOfApproval>();
        public virtual ICollection<RIVariation>           Variations        { get; set; } = new List<RIVariation>();
        public virtual ICollection<RIAccessPathwayRecord> AccessPathways    { get; set; } = new List<RIAccessPathwayRecord>();
        public virtual ICollection<RIDocument>            Documents         { get; set; } = new List<RIDocument>();
        public virtual ICollection<RIAuditLog>            AuditLogs         { get; set; } = new List<RIAuditLog>();
        public virtual ICollection<RegulatoryApplication> ChildApplications { get; set; } = new List<RegulatoryApplication>();
        public virtual ICollection<RIApprovalSnapshot>    ApprovalSnapshots { get; set; } = new List<RIApprovalSnapshot>();
    }
}
