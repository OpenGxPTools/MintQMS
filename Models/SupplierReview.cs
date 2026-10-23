using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class SupplierReview
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        [Display(Name = "Review Date")]
        public DateTime ReviewDate { get; set; }

        [Display(Name = "Review Trigger")]
        public ReviewTrigger ReviewTrigger { get; set; }

        [Display(Name = "Status Before Review")]
        public SupplierStatus StatusBefore { get; set; }

        [Display(Name = "Status After Review")]
        public SupplierStatus StatusAfter { get; set; }

        [Display(Name = "Reason for Review")]
        public string? ReviewSummary { get; set; }

        [Display(Name = "Outcome / Decision Rationale")]
        public string? Outcome { get; set; }

        // Qualification fields — only used when StatusAfter == Qualified
        [Display(Name = "Qualification Justification")]
        public string? QualificationJustification { get; set; }

        [Display(Name = "Qualification Basis")]
        public QualificationBasis? QualificationBasis { get; set; }

        [Display(Name = "Qualification Reference Document")]
        public string? QualificationReferenceDoc { get; set; }

        // Suspension/Cancellation fields
        [Display(Name = "Suspension Reason")]
        public string? SuspensionReason { get; set; }

        [Display(Name = "Cancellation Reason")]
        public string? CancellationReason { get; set; }

        [Display(Name = "Next Review Date")]
        public DateTime? NextReviewDate { get; set; }

        // Review status — allows two-person review before it takes effect
        [Display(Name = "Review Status")]
        public SupplierReviewStatus ReviewStatus { get; set; }

        // E-signature fields
        [Display(Name = "Reviewed By")]
        public string? ReviewedBy { get; set; }

        [Display(Name = "Reviewed On")]
        public DateTime ReviewedOn { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }
    }

    public enum SupplierReviewStatus
    {
        Draft,
        [Display(Name = "Under Review")]
        UnderReview,
        Completed
    }
}
