using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models.LC
{
    public class LicenceCertificationRecord
    {
        public int Id { get; set; }

        // ── Identification ────────────────────────────────────────────────────
        [Required, MaxLength(20)]
        [Display(Name = "LC Number")]
        public string LCNumber { get; set; } = string.Empty;           // LC-YYYY-NNNN

        public LCHolderType HolderType { get; set; } = LCHolderType.OwnCompany;

        [Required, MaxLength(300)]
        [Display(Name = "Holder Name")]
        public string HolderName { get; set; } = string.Empty;        // own company name OR third-party site name

        // Link to Supplier module (nullable — for third-party records where supplier exists)
        public int? SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        [MaxLength(500)]
        [Display(Name = "Linked Site Address")]
        public string? LinkedSiteAddress { get; set; }

        [MaxLength(100)]
        [Display(Name = "Linked Site Country")]
        public string? LinkedSiteCountry { get; set; }

        // ── Classification ────────────────────────────────────────────────────
        public LCCategory Category { get; set; }

        public int LicenceTypeId { get; set; }

        [ForeignKey("LicenceTypeId")]
        public virtual LCLicenceType LicenceType { get; set; } = null!;

        [MaxLength(300)]
        [Display(Name = "Issuing Body")]
        public string? IssuingBody { get; set; }

        [MaxLength(500)]
        [Display(Name = "Issuing Body URL")]
        public string? IssuingBodyUrl { get; set; }

        [MaxLength(500)]
        [Display(Name = "Issuing Body Portal")]
        public string? IssuingBodyPortal { get; set; }

        // ── Status & Workflow ─────────────────────────────────────────────────
        public LCStatus Status { get; set; } = LCStatus.Draft;

        [Display(Name = "Locked")]
        public bool IsLocked { get; set; } = false;

        // ── Licence Details ───────────────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Licence / Certificate Number")]
        public string? LicenceNumber { get; set; }                     // authority's reference number

        [Display(Name = "Scope")]
        public string? Scope { get; set; }                             // activities/products/dosage forms covered

        [Display(Name = "Application Date")]
        public DateTime? ApplicationDate { get; set; }

        [Display(Name = "Issue Date")]
        public DateTime? IssueDate { get; set; }

        [Display(Name = "Expiry Date")]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "Next Renewal Due")]
        public DateTime? NextRenewalDue { get; set; }

        [Display(Name = "Renewal Lead Time (days)")]
        public int RenewalLeadTimeDays { get; set; } = 90;

        // ── GMP Clearance Specifics (Category C / ThirdPartySiteApproval) ────
        [Display(Name = "GMP Clearance Pathway")]
        public LCGmcPathway? GmcPathway { get; set; }

        [MaxLength(100)]
        [Display(Name = "Site Country")]
        public string? SiteCountry { get; set; }

        [Display(Name = "Manufacturing Steps (Scope)")]
        public string? ManufacturingStepsScope { get; set; }

        [Display(Name = "Last Inspection Date")]
        public DateTime? LastInspectionDate { get; set; }

        [MaxLength(200)]
        [Display(Name = "Inspection Authority")]
        public string? InspectionAuthority { get; set; }

        [Display(Name = "RFI Date")]
        public DateTime? RFIDate { get; set; }

        [Display(Name = "RFI Response Due Date")]
        public DateTime? RFIResponseDueDate { get; set; }

        [Display(Name = "RFI Response Submitted Date")]
        public DateTime? RFIResponseSubmittedDate { get; set; }

        [Display(Name = "Letter of Access Available")]
        public bool LetterOfAccessAvailable { get; set; } = false;

        // ── ISO Cert Specifics (Category B) ──────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Certification Body")]
        public string? CertificationBody { get; set; }

        [MaxLength(200)]
        [Display(Name = "Certification Body Accreditation")]
        public string? CertificationBodyAccreditation { get; set; }   // JAS-ANZ accreditation number

        [MaxLength(100)]
        [Display(Name = "Standard Version")]
        public string? StandardVersion { get; set; }

        [Display(Name = "Certificate Scope")]
        public string? CertificateScope { get; set; }

        [Display(Name = "Stage 1 Audit Date")]
        public DateTime? Stage1AuditDate { get; set; }

        [Display(Name = "Stage 2 Audit Date")]
        public DateTime? Stage2AuditDate { get; set; }

        [Display(Name = "Next Surveillance Audit Date")]
        public DateTime? NextSurveillanceAuditDate { get; set; }

        [Display(Name = "Recertification Due Date")]
        public DateTime? RecertificationDueDate { get; set; }

        // ── Contacts ──────────────────────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Internal Responsible Person")]
        public string? InternalResponsiblePersonId { get; set; }       // AppUser.Id (string FK)

        [MaxLength(200)]
        [Display(Name = "External Consultant")]
        public string? ExternalConsultant { get; set; }

        [MaxLength(200)]
        [Display(Name = "Authority Contact Email")]
        public string? AuthorityContactEmail { get; set; }

        [MaxLength(200)]
        [Display(Name = "Authority Portal Username")]
        public string? AuthorityPortalUsername { get; set; }

        // ── Fees ──────────────────────────────────────────────────────────────
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Application Fee")]
        public decimal? ApplicationFee { get; set; }

        [MaxLength(10)]
        [Display(Name = "Fee Currency")]
        public string FeeCurrency { get; set; } = "AUD";

        [Display(Name = "Fee Paid Date")]
        public DateTime? FeePaidDate { get; set; }

        [MaxLength(200)]
        [Display(Name = "Fee Reference")]
        public string? FeeReference { get; set; }

        // ── Justification / Reason (for NotApplicable, Suspended, Withdrawn) ─
        [Display(Name = "Justification / Reason")]
        public string? JustificationReason { get; set; }

        // ── Regulatory action notes (overseas site warnings, import alerts) ──
        [Display(Name = "Regulatory Action Notes")]
        public string? RegulatoryActionNotes { get; set; }

        // ── Audit trail fields ────────────────────────────────────────────────
        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public bool      IsDeleted   { get; set; } = false;
        public string?   DeletedBy   { get; set; }
        public DateTime? DeletedOn   { get; set; }
        public DateTime? SuspendedOn { get; set; }
        public DateTime? WithdrawnOn { get; set; }

        // ── Gap 13: Retention & Disposal ─────────────────────────────────────
        public DateTime?          RetainUntil                   { get; set; }
        public bool               RetentionExtended             { get; set; } = false;
        public RetentionHoldReason? RetentionExtendedReason     { get; set; }
        public string?            RetentionExtendedComments     { get; set; }
        public string?            RetentionExtendedBy           { get; set; }
        public DateTime?          RetentionExtendedOn           { get; set; }
        public string?            RetentionExtendedRemovalReason { get; set; }
        public string?            RetentionExtendedRemovedBy    { get; set; }
        public DateTime?          RetentionExtendedRemovedOn    { get; set; }
        public DateTime?          RetentionHoldCooldownUntil    { get; set; }

        public int RenewalCycle { get; set; } = 0;

        // ── Navigation properties ─────────────────────────────────────────────
        public virtual ICollection<LCCondition>        Conditions       { get; set; } = new List<LCCondition>();
        public virtual ICollection<LCDocument>         Documents        { get; set; } = new List<LCDocument>();
        public virtual ICollection<LCDependency>       Dependencies     { get; set; } = new List<LCDependency>();
        public virtual ICollection<LCAuditLog>         AuditLogs        { get; set; } = new List<LCAuditLog>();
        public virtual ICollection<LCApprovalSnapshot> ApprovalSnapshots { get; set; } = new List<LCApprovalSnapshot>();
    }
}
