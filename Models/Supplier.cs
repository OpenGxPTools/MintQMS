using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Display(Name = "Supplier ID")]
        public string SupplierCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Legal Name")]
        public string LegalName { get; set; } = string.Empty;

        [Display(Name = "Trading Name")]
        public string? TradingName { get; set; }

        [Display(Name = "Legal ID (ABN/ACN/Other)")]
        public string? LegalId { get; set; }

        [Display(Name = "Website")]
        public string? Website { get; set; }

        // Address
        [Display(Name = "Street")]
        public string? Street { get; set; }

        [Display(Name = "City")]
        public string? City { get; set; }

        [Display(Name = "State")]
        public string? State { get; set; }

        [Display(Name = "Post Code")]
        public string? PostCode { get; set; }

        [Display(Name = "Country")]
        public string? Country { get; set; }

        // Primary Contact
        [Display(Name = "Contact Name")]
        public string? ContactName { get; set; }

        [Display(Name = "Contact Email")]
        public string? ContactEmail { get; set; }

        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        // Classification
        [Display(Name = "Supplier Type")]
        public SupplierType SupplierType { get; set; }

        [Display(Name = "Risk Level")]
        public SupplierRiskLevel RiskLevel { get; set; } = SupplierRiskLevel.NotAssessed;

        [Display(Name = "Risk Justification")]
        public string? RiskJustification { get; set; }

        // Qualification pathway
        [Display(Name = "Qualification Required")]
        public bool QualificationRequired { get; set; } = false;

        [Display(Name = "Testing Requirement")]
        public TestingRequirement TestingRequirement { get; set; }

        // Qualification fields
        [Display(Name = "Qualification Justification")]
        public string? QualificationJustification { get; set; }

        [Display(Name = "Qualification Basis")]
        public QualificationBasis? QualificationBasis { get; set; }

        [Display(Name = "Qualification Reference Document")]
        public string? QualificationReferenceDoc { get; set; }

        // Review/expiry tracking
        [Display(Name = "Review Frequency")]
        public ReviewFrequency ReviewFrequency { get; set; }

        [Display(Name = "Next Review Date")]
        public DateTime? NextReviewDate { get; set; }

        [Display(Name = "Last Review Date")]
        public DateTime? LastReviewDate { get; set; }

        [Display(Name = "Review Trigger")]
        public ReviewTrigger ReviewTrigger { get; set; }

        [Display(Name = "Effective Date")]
        public DateTime? EffectiveDate { get; set; }

        // Optional fixed end of the approval (time-limited / conditional approvals), set at
        // Approve/Qualify. Blank = approval valid until the next review.
        [Display(Name = "Approval Expiry")]
        public DateTime? ExpiryDate { get; set; }

        // Mandatory when ExpiryDate is set: why the approval is time-limited / its condition.
        [Display(Name = "Approval Expiry Reason")]
        [StringLength(1000)]
        public string? ApprovalExpiryReason { get; set; }

        // Workflow tracking
        [Display(Name = "Assessed By")]
        public string? AssessedBy { get; set; }

        [Display(Name = "Assessed On")]
        public DateTime? AssessedOn { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }

        [Display(Name = "Approval Comments")]
        public string? ApprovalComments { get; set; }

        [Display(Name = "Qualified By")]
        public string? QualifiedBy { get; set; }

        [Display(Name = "Qualified On")]
        public DateTime? QualifiedOn { get; set; }

        [Display(Name = "Qualification Comments")]
        public string? QualificationComments { get; set; }

        [Display(Name = "Suspended By")]
        public string? SuspendedBy { get; set; }

        [Display(Name = "Suspended On")]
        public DateTime? SuspendedOn { get; set; }

        [Display(Name = "Suspension Reason")]
        public string? SuspensionReason { get; set; }

        [Display(Name = "Cancelled By")]
        public string? CancelledBy { get; set; }

        [Display(Name = "Cancelled On")]
        public DateTime? CancelledOn { get; set; }

        [Display(Name = "Cancellation Reason")]
        public string? CancellationReason { get; set; }

        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        public SupplierStatus Status { get; set; }

        [Display(Name = "Approval Status")]
        public SupplierStatus? ApprovalStatus { get; set; }

        public string? ExportFilePath { get; set; }

        // Navigation
        public ICollection<SupplierDocument>? Documents { get; set; }
        public ICollection<SupplierQualityRecord>? QualityRecords { get; set; }
        public ICollection<SupplierAddress>? Addresses { get; set; }
        public ICollection<SupplierReview>? Reviews { get; set; }
        public ICollection<SupplierAuditLog>? AuditLogs { get; set; }
        public ICollection<SupplierSnapshot>? Snapshots { get; set; }
        public ICollection<SupplierMaterial>? SupplierMaterials { get; set; }
        public ICollection<SupplierProduct>? SupplierProducts { get; set; }
        public ICollection<SupplierService>? SupplierServices { get; set; }

        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
    }

    public enum SupplierStatus
    {
        Pending,
        [Display(Name = "Under Assessment")]
        UnderAssessment,
        [Display(Name = "Pending Suspension")]
        PendingSuspension,
        [Display(Name = "Pending Cancellation")]
        PendingCancellation,
        Approved,
        Qualified,
        Suspended,
        Cancelled,
        Rejected
    }

    public enum SupplierType
    {
        Product,
        Material,
        Service
    }

    public enum SupplierRiskLevel
    {
        Critical,
        High,
        Medium,
        Low,
        NotAssessed = 4
    }

    public enum TestingRequirement
    {
        FullTesting,
        ReducedTesting
    }

    public enum QualificationBasis
    {
        AuditResult,
        PerformanceHistory,
        ThirdPartyCertification,
        RegulatoryApproval,
        Other
    }

    public enum ReviewFrequency
    {
        Annual,
        Biannual,
        ThreeYearly,
        Discretional
    }

    public enum ReviewTrigger
    {
        Scheduled,
        Deviation,
        Complaint,
        Audit,
        Risk,
        Change,
        Other
    }
}
