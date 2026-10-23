using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class PVSignalReview
    {
        public int Id { get; set; }

        [Display(Name = "MedDRA Preferred Term")]
        public string MedDRAPT { get; set; } = string.Empty;

        [Display(Name = "Detected On")]
        public DateTime DetectedOn { get; set; }

        [Display(Name = "Detected By")]
        public string DetectedBy { get; set; } = string.Empty;

        [Display(Name = "Trigger Count")]
        public int TriggerCount { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [Display(Name = "Assessment Notes")]
        public string? AssessmentNotes { get; set; }

        [Display(Name = "Reviewed On")]
        public DateTime? ReviewedOn { get; set; }

        [Display(Name = "Reviewed By")]
        public string? ReviewedBy { get; set; }

        [Display(Name = "Conclusion")]
        public SignalConclusion? Conclusion { get; set; }

        public SignalStatus Status { get; set; } = SignalStatus.Open;

        // Optional link to CAPA if action was taken
        [Display(Name = "Linked CAPA ID")]
        public int? LinkedCapaId { get; set; }

        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        public virtual ICollection<PVSignalAuditLog>? AuditLogs { get; set; }

        public enum SignalConclusion
        {
            Confirmed,
            Refuted,
            [Display(Name = "Ongoing Monitoring")]
            Ongoing
        }

        public enum SignalStatus
        {
            Open,
            [Display(Name = "Under Review")]
            UnderReview,
            Confirmed,
            Refuted,
            Closed
        }
    }
}
