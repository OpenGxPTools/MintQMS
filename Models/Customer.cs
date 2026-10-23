using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Display(Name = "ID")]
        public string CustomerId { get; set; } = string.Empty;

        public string? GeneratedFrom { get; set; }

        [Required]
        [Display(Name = "Legal Name")]
        public string LegalName { get; set; } = string.Empty;

        [Display(Name = "Trading Name")]
        public string? TradingName { get; set; }

        [Display(Name = "Customer Type")]
        public CustomerType CustomerType { get; set; }

        [Display(Name = "Industry / Sector")]
        public string? Industry { get; set; }

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

        // Commercial
        [Display(Name = "Account Number")]
        public string? AccountNumber { get; set; }

        [Display(Name = "Contract Reference")]
        public string? ContractReference { get; set; }

        [Display(Name = "Contract Expiry Date")]
        public DateTime? ContractExpiryDate { get; set; }

        // Notes
        [Display(Name = "Special Requirements")]
        public string? SpecialRequirements { get; set; }

        // Versioning / workflow
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

        // Navigation
        public virtual ICollection<CustomerDocument>? Documents { get; set; }
        public virtual ICollection<CustomerLinkedDocument>? RelatedDocuments { get; set; }
        public virtual ICollection<Complaint>? Complaints { get; set; }

        public CustomerStatus Status { get; set; }

        public enum CustomerStatus
        {
            Draft,
            Approved,
            Obsolete,
            [Display(Name = "Under Review")]
            UnderReview
        }
    }

    public enum CustomerType
    {
        Distributor,
        EndUser,
        OEM,
        Retailer,
        Government,
        Other,
        [Display(Name = "Hospital / Clinic")]
        HospitalClinic,
        CRO
    }
}
