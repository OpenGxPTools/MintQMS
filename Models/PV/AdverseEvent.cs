using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenQMS.Models.RI;

namespace OpenQMS.Models
{
    public class AdverseEvent
    {
        public int Id { get; set; }

        [Display(Name = "AE Number")]
        public string AENumber { get; set; } = string.Empty;

        [Display(Name = "Report Date")]
        public DateTime ReportDate { get; set; }

        [Display(Name = "Day 0 Date")]
        public DateTime? Day0Date { get; set; }

        [Display(Name = "TGA Deadline Date")]
        public DateTime? TGADeadlineDate { get; set; }

        [Display(Name = "Report Type")]
        public AEReportType ReportType { get; set; }

        [Display(Name = "Report Source")]
        public AEReportSource ReportSource { get; set; }

        [Display(Name = "Parent AE")]
        public int? ParentAEId { get; set; }

        [ForeignKey("ParentAEId")]
        public virtual AdverseEvent? ParentAE { get; set; }

        public virtual ICollection<AdverseEvent>? FollowUpReports { get; set; }

        // Cross-module links
        [Display(Name = "Linked Access Pathway")]
        public int? AccessPathwayRecordId { get; set; }

        [ForeignKey("AccessPathwayRecordId")]
        public virtual RIAccessPathwayRecord? AccessPathway { get; set; }

        [Display(Name = "SAS Reference Number")]
        [MaxLength(100)]
        public string? SASReferenceNumber { get; set; }

        [Display(Name = "Linked Complaint")]
        public int? LinkedComplaintId { get; set; }

        // Relationship configured in ApplicationDbContext.OnModelCreating (independent
        // one-way link; FK also exists on Complaint.LinkedAEId, kept in sync by controllers).
        public virtual Complaint? LinkedComplaint { get; set; }

        [Display(Name = "Linked CAPA")]
        public int? LinkedCapaId { get; set; }

        // Relationship configured in ApplicationDbContext.OnModelCreating (independent
        // one-way link; FK also exists on Capa.LinkedAdverseEventId, kept in sync by controllers).
        public virtual Capa? LinkedCapa { get; set; }

        public AEStatus Status { get; set; }

        [Display(Name = "Is Serious")]
        public bool IsSerious { get; set; }

        [Display(Name = "Seriousness Criteria")]
        public SeriousnessFlags SeriousnessCriteria { get; set; }

        [Display(Name = "Fatal or Life-Threatening")]
        public bool IsFatalOrLifeThreatening { get; set; }

        [Display(Name = "Reportable to TGA")]
        public bool? IsReportable { get; set; }

        [Display(Name = "Record Locked")]
        public bool IsLocked { get; set; }

        // AE Details
        [Display(Name = "AE Description")]
        public string? AEDescription { get; set; }

        [Display(Name = "MedDRA Preferred Term")]
        public string? MedDRAPT { get; set; }

        [Display(Name = "MedDRA System Organ Class")]
        public string? MedDRASOC { get; set; }

        [Display(Name = "AE Start Date")]
        public DateTime? AEStartDate { get; set; }

        [Display(Name = "AE End Date")]
        public DateTime? AEEndDate { get; set; }

        [Display(Name = "Outcome")]
        public AEOutcome? Outcome { get; set; }

        [Display(Name = "Causality")]
        public AECausality? Causality { get; set; }

        [Display(Name = "Severity")]
        public AESeverity? Severity { get; set; }

        [Display(Name = "Expectedness")]
        public AEExpectedness? Expectedness { get; set; }

        // Case Assessment
        [Display(Name = "Medical Narrative")]
        public string? MedicalNarrative { get; set; }

        [Display(Name = "Medical Evaluator")]
        public string? MedicalEvaluator { get; set; }

        [Display(Name = "Evaluation Date")]
        public DateTime? EvaluationDate { get; set; }

        [Display(Name = "Signal Detected")]
        public bool SignalDetected { get; set; }

        // Audit fields
        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = string.Empty;

        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        [Display(Name = "Modified By")]
        public string? ModifiedBy { get; set; }

        [Display(Name = "Modified On")]
        public DateTime? ModifiedOn { get; set; }

        // Navigation
        public virtual AEReporter? Reporter { get; set; }
        public virtual AEPatient? Patient { get; set; }
        public virtual AESuspectProduct? SuspectProduct { get; set; }
        public virtual AERegulatoryReport? RegulatoryReport { get; set; }
        public virtual ICollection<AEAttachment>? Attachments { get; set; }

        // Enums
        public enum AEStatus
        {
            Draft,
            [Display(Name = "Under Medical Review")]
            UnderMedicalReview,
            [Display(Name = "Pending Approval")]
            PendingApproval,
            Approved,
            Closed,
            Voided
        }

        public enum AEReportType
        {
            Initial,
            [Display(Name = "Follow-Up")]
            FollowUp
        }

        public enum AEReportSource
        {
            Spontaneous,
            Literature,
            [Display(Name = "Clinical Study")]
            ClinicalStudy,
            [Display(Name = "Post-Market Study")]
            PostMarketStudy,
            [Display(Name = "Digital Media / Social Media")]
            DigitalMedia,
            [Display(Name = "Regulatory Authority")]
            RegulatoryAuthority,
            Other
        }

        [Flags]
        public enum SeriousnessFlags
        {
            None = 0,
            Death = 1,
            [Display(Name = "Life-Threatening")]
            LifeThreatening = 2,
            Hospitalisation = 4,
            [Display(Name = "Disability / Incapacity")]
            Disability = 8,
            [Display(Name = "Congenital Anomaly / Birth Defect")]
            CongenitalAnomaly = 16,
            [Display(Name = "Medically Important")]
            MedicallyImportant = 32
        }

        public enum AEOutcome
        {
            [Display(Name = "Recovered / Resolved")]
            Recovered,
            [Display(Name = "Recovering / Resolving")]
            Recovering,
            [Display(Name = "Not Recovered / Ongoing")]
            NotRecovered,
            [Display(Name = "Recovered with Sequelae")]
            RecoveredWithSequelae,
            Fatal,
            Unknown
        }

        public enum AECausality
        {
            Certain,
            [Display(Name = "Probable / Likely")]
            Probable,
            Possible,
            Unlikely,
            [Display(Name = "Unclassifiable / Unassessable")]
            Unclassifiable,
            [Display(Name = "Not Applicable")]
            NotApplicable
        }

        public enum AESeverity
        {
            Mild,
            Moderate,
            Severe,
            [Display(Name = "Life-Threatening")]
            LifeThreatening,
            Fatal
        }

        public enum AEExpectedness
        {
            [Display(Name = "Expected (listed in PI)")]
            Expected,
            [Display(Name = "Unexpected (not in PI)")]
            Unexpected,
            Unknown
        }
    }
}
