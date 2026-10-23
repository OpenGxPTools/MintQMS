using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Display(Name = "ID")]
        public string ServiceId { get; set; }

        public string? GeneratedFrom { get; set; }

        [Display(Name = "Name")]
        [Required]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Category")]
        public ServiceCategory Category { get; set; }

        public decimal Version { get; set; }

        [Display(Name = "Edited By")]
        public string? EditedBy { get; set; }

        [Display(Name = "Edited On")]
        public DateTime EditedOn { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }

        public bool IsLocked { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }

        public string? ExportFilePath { get; set; }

        public virtual ICollection<ServiceDocument>? Documents { get; set; }
        public virtual ICollection<ServiceLinkedDocument>? RelatedDocuments { get; set; }

        [Display(Name = "Status")]
        public ServiceStatus Status { get; set; }

        public enum ServiceCategory
        {
            Laboratory,
            [Display(Name = "Logistics / Transportation")]
            LogisticsTransportation,
            Manufacturing,
            Packaging,
            [Display(Name = "Consulting / Professional Services")]
            ConsultingProfessionalServices,
            Maintenance,
            Calibration,
            [Display(Name = "Validation / Qualification")]
            ValidationQualification,
            [Display(Name = "Regulatory / Compliance")]
            RegulatoryCompliance,
            [Display(Name = "Quality/GMP Services")]
            QualityGMPServices,
            [Display(Name = "Cleaning / Sanitation")]
            CleaningSanitation,
            Sterilisation,
            [Display(Name = "Warehousing / Storage")]
            WarehousingStorage,
            [Display(Name = "IT / Computer Systems")]
            ITComputerSystems,
            [Display(Name = "Engineering / Technical Services")]
            EngineeringTechnicalServices,
            [Display(Name = "Waste Management")]
            WasteManagement,
            Training,
            [Display(Name = "Inspection / Testing")]
            InspectionTesting,
            [Display(Name = "Cold Chain Services")]
            ColdChainServices,
            Other
        }

        public enum ServiceStatus
        {
            Draft,
            Approved,
            Obsolete,
            [Display(Name = "Under Review")]
            UnderReview
        }
    }
}
