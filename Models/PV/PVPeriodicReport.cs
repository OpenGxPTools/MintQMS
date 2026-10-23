using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class PVPeriodicReport
    {
        public int Id { get; set; }

        [Display(Name = "PSUR Title")]
        public string Title { get; set; } = string.Empty;

        public int? ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Display(Name = "Period Start")]
        public DateTime? PeriodStart { get; set; }

        [Display(Name = "Period End")]
        public DateTime? PeriodEnd { get; set; }

        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Submission Date")]
        public DateTime? SubmissionDate { get; set; }

        [Display(Name = "TGA Reference Number")]
        public string? TGAReferenceNumber { get; set; }

        public PSURStatus Status { get; set; }

        [Display(Name = "Exposure Data")]
        public string? ExposureData { get; set; }

        [Display(Name = "Benefit-Risk Conclusion")]
        public string? BenefitRiskConclusion { get; set; }

        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = string.Empty;

        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        public virtual ICollection<AERegulatoryReport>? LinkedAECases { get; set; }
        public virtual ICollection<PVPsurAuditLog>? AuditLogs { get; set; }
        public virtual ICollection<PSURAttachment>? Attachments { get; set; }

        public enum PSURStatus
        {
            [Display(Name = "In Preparation")]
            InPreparation,
            [Display(Name = "Under Review")]
            UnderReview,
            Submitted,
            Accepted
        }
    }
}
