using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AEReporter
    {
        public int Id { get; set; }

        public int AdverseEventId { get; set; }

        [ForeignKey("AdverseEventId")]
        public virtual AdverseEvent? AdverseEvent { get; set; }

        [Display(Name = "Reporter Name / Initials")]
        public string? ReporterName { get; set; }

        [Display(Name = "Reporter Type")]
        public ReporterType? Type { get; set; }

        public string? Institution { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? Country { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        [Display(Name = "Qualification")]
        public ReporterQualification? Qualification { get; set; }

        [Display(Name = "Date of Report to Reporter")]
        public DateTime? ReportDate { get; set; }

        public enum ReporterType
        {
            [Display(Name = "Healthcare Professional")]
            HealthcareProfessional,
            Consumer,
            Patient,
            [Display(Name = "Regulatory Authority")]
            RegulatoryAuthority,
            Other
        }

        public enum ReporterQualification
        {
            Physician,
            Pharmacist,
            [Display(Name = "Other Health Professional")]
            OtherHealthProfessional,
            Lawyer,
            Consumer,
            Other
        }
    }
}
