using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class SupplierSnapshot
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        [Display(Name = "Snapshot Date")]
        public DateTime SnapshotDate { get; set; }
        [Display(Name = "Completed By")]
        public string? CompletedBy { get; set; }

        // Identity
        public string SupplierCode { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string? TradingName { get; set; }
        public string? LegalId { get; set; }
        public string? Website { get; set; }

        // Address
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostCode { get; set; }
        public string? Country { get; set; }

        // Contact
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }

        // Classification
        public SupplierType SupplierType { get; set; }
        public string? LinkedItems { get; set; }

        // Assessment
        public SupplierRiskLevel RiskLevel { get; set; }
        public string? RiskJustification { get; set; }
        public string? AssessedBy { get; set; }
        public DateTime? AssessedOn { get; set; }

        // Approval
        public SupplierStatus ApprovalStatus { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string? ApprovalComments { get; set; }
        public ReviewFrequency ReviewFrequency { get; set; }
        public bool QualificationRequired { get; set; }
        public DateTime? NextReviewDate { get; set; }
        public DateTime? ExpiryDate { get; set; }   // approval expiry at the time of the snapshot
        public string? ApprovalExpiryReason { get; set; }

        // Qualification
        public string? QualifiedBy { get; set; }
        public DateTime? QualifiedOn { get; set; }
        public QualificationBasis? QualificationBasis { get; set; }
        public string? QualificationJustification { get; set; }
        public string? QualificationReferenceDoc { get; set; }
        public string? QualificationComments { get; set; }

        // Suspension/Cancellation
        public string? SuspendedBy { get; set; }
        public DateTime? SuspendedOn { get; set; }
        public string? SuspensionReason { get; set; }
        public string? CancelledBy { get; set; }
        public DateTime? CancelledOn { get; set; }
        public string? CancellationReason { get; set; }

        // Review context (for re-assessments)
        public ReviewTrigger? ReviewTrigger { get; set; }
        public string? ReasonForReview { get; set; }
        public int? LinkedQualityRecordId { get; set; }
        public string? LinkedQualityRecordCode { get; set; }
        public string? LinkedQualityRecordTitle { get; set; }

        // Frozen children
        public ICollection<SupplierSnapshotDocument>? Documents { get; set; }
        public ICollection<SupplierSnapshotQualityRecord>? QualityRecords { get; set; }
        public ICollection<SupplierSnapshotAddress>? Addresses { get; set; }
    }

    public class SupplierSnapshotDocument
    {
        public int Id { get; set; }
        public int SnapshotId { get; set; }
        [ForeignKey("SnapshotId")]
        public virtual SupplierSnapshot? Snapshot { get; set; }
        public SupplierDocumentType DocumentType { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public string? Description { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime UploadedDate { get; set; }
    }

    public class SupplierSnapshotQualityRecord
    {
        public int Id { get; set; }
        public int SnapshotId { get; set; }
        [ForeignKey("SnapshotId")]
        public virtual SupplierSnapshot? Snapshot { get; set; }
        public QualityRecordType RecordType { get; set; }
        public int? DeviationId { get; set; }
        public int? CapaId { get; set; }
        public int? ChangeId { get; set; }
        public int? ComplaintId { get; set; }
        public int? RiskId { get; set; }
        public int? AuditId { get; set; }
        public string? RecordCode { get; set; }
        public string? RecordTitle { get; set; }
        public string? Notes { get; set; }
        public string? LinkedBy { get; set; }
        public DateTime LinkedOn { get; set; }
    }

    public class SupplierSnapshotAddress
    {
        public int Id { get; set; }
        public int SnapshotId { get; set; }
        [ForeignKey("SnapshotId")]
        public virtual SupplierSnapshot? Snapshot { get; set; }
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostCode { get; set; }
        public string? Country { get; set; }
        public string? Notes { get; set; }
    }
}
