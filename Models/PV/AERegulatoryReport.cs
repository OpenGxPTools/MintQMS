using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AERegulatoryReport
    {
        public int Id { get; set; }

        public int AdverseEventId { get; set; }

        [ForeignKey("AdverseEventId")]
        public virtual AdverseEvent? AdverseEvent { get; set; }

        [Display(Name = "Reportable to TGA")]
        public bool? IsReportableToTGA { get; set; }

        [Display(Name = "Reporting Decision Rationale")]
        public string? ReportingDecisionRationale { get; set; }

        [Display(Name = "TGA Submission Date")]
        public DateTime? TGASubmissionDate { get; set; }

        [Display(Name = "TGA Submission Method")]
        public TGASubmissionMethod? SubmissionMethod { get; set; }

        [Display(Name = "TGA ADRS Reference Number")]
        public string? TGAADRSReferenceNumber { get; set; }

        [Display(Name = "E2B Transmission Reference")]
        public string? E2BTransmissionReference { get; set; }

        [Display(Name = "7-Day Deadline (Fatal/Life-Threatening)")]
        public DateTime? SevenDayDeadlineDate { get; set; }

        [Display(Name = "Overseas Reporting Required")]
        public bool OverseasReportingRequired { get; set; }

        [Display(Name = "Overseas Authorities")]
        public string? OverseasAuthorities { get; set; }

        [Display(Name = "Include in Next PSUR")]
        public bool IncludeInNextPSUR { get; set; }

        [Display(Name = "Significant Safety Issue")]
        public bool SignificantSafetyIssue { get; set; }

        [Display(Name = "Regulatory Status")]
        public AERegulatoryStatus Status { get; set; }

        // Link to PSUR period
        public int? PSURId { get; set; }

        [ForeignKey("PSURId")]
        public virtual PVPeriodicReport? PSUR { get; set; }

        public enum TGASubmissionMethod
        {
            [Display(Name = "AEMS Portal")]
            AEMSPortal,
            [Display(Name = "E2B(R3) EDI")]
            E2BEDI,
            [Display(Name = "CIOMS-I Email")]
            CIOMSEmail
        }

        public enum AERegulatoryStatus
        {
            [Display(Name = "Not Required")]
            NotRequired,
            [Display(Name = "Pending Submission")]
            PendingSubmission,
            [Display(Name = "Submitted to TGA")]
            SubmittedToTGA,
            [Display(Name = "Acknowledged by TGA")]
            AcknowledgedByTGA,
            [Display(Name = "Follow-up Submitted")]
            FollowUpSubmitted,
            Closed
        }
    }
}
