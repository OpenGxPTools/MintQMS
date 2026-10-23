using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models.Release
{
    public class ReleaseRecord
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string RecordNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Release Type")]
        public ReleaseType ReleaseType { get; set; }

        public ReleaseStatus Status { get; set; } = ReleaseStatus.Draft;

        // ── Product Identification ──────────────────────────────────
        [Required, MaxLength(200)]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Product Type")]
        public OpenQMS.Models.ProductType? ProductType { get; set; }

        [MaxLength(200)]
        [Display(Name = "Brand / Trade Name")]
        public string? BrandName { get; set; }

        [MaxLength(200)]
        [Display(Name = "INN / Generic Name")]
        public string? InnName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Dosage Form")]
        public string? DosageForm { get; set; }

        [MaxLength(100)]
        [Display(Name = "Strength / Presentation")]
        public string? Strength { get; set; }

        [MaxLength(100)]
        [Display(Name = "ARTG Entry / Regulatory Ref.")]
        public string? ArtgReference { get; set; }

        // ── Product-type-specific fields ────────────────────────────
        [MaxLength(300)]
        [Display(Name = "INCI Name / Ingredient Declaration Reference")]
        public string? InciName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Presentation / Form")]
        public string? PresentationForm { get; set; }

        [MaxLength(100)]
        [Display(Name = "Device Type / Category")]
        public string? DeviceTypeCategory { get; set; }

        [MaxLength(100)]
        [Display(Name = "Model / Version")]
        public string? ModelVersion { get; set; }

        [MaxLength(200)]
        [Display(Name = "Standard / Scientific Name")]
        public string? StandardScientificName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Material Type / Category")]
        public string? RawMaterialTypeCategory { get; set; }

        [MaxLength(100)]
        [Display(Name = "Grade (if applicable)")]
        public string? Grade { get; set; }

        // ── Batch Details ───────────────────────────────────────────
        [Required, MaxLength(100)]
        [Display(Name = "Batch / Lot Number")]
        public string BatchNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Batch Size")]
        public string? BatchSize { get; set; }

        [MaxLength(100)]
        [Display(Name = "Container / Pack Size")]
        public string? PackSize { get; set; }

        [Display(Name = "Manufacturing Date")]
        public DateTime? ManufacturingDate { get; set; }

        [Display(Name = "Expiry Date")]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "Retest Date")]
        public DateTime? RetestDate { get; set; }

        // ── Sites ───────────────────────────────────────────────────
        [Required, MaxLength(500)]
        [Display(Name = "Manufacturing Site")]
        public string ManufacturingSite { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Manufacturing Site GMP Licence / Clearance No.")]
        public string? ManufacturingSiteGmpRef { get; set; }

        [MaxLength(500)]
        [Display(Name = "Packaging Site")]
        public string? PackagingSite { get; set; }

        [MaxLength(200)]
        [Display(Name = "Packaging Site GMP Licence / Clearance No.")]
        public string? PackagingSiteGmpRef { get; set; }

        [MaxLength(500)]
        [Display(Name = "Releasing Company")]
        public string? ReleasingCompany { get; set; }

        [MaxLength(200)]
        [Display(Name = "Releasing Company TGA Licence / Clearance No.")]
        public string? ReleasingCompanyLicenceRef { get; set; }

        // ── For Release to Customer / RFFP ──────────────────────────
        [MaxLength(500)]
        [Display(Name = "Responsible Party for Final RFS")]
        public string? FinalReleaseParty { get; set; }

        [MaxLength(200)]
        [Display(Name = "Technical Agreement Reference")]
        public string? TechnicalAgreementRef { get; set; }

        // ── Specification ───────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Product Specification File Reference")]
        public string? SpecificationReference { get; set; }

        [Display(Name = "Linked Product (Master Data)")]
        public int? LinkedProductId { get; set; }

        // ── Storage & Distribution ──────────────────────────────────
        [MaxLength(300)]
        [Display(Name = "Storage Conditions")]
        public string? StorageConditions { get; set; }

        [MaxLength(500)]
        [Display(Name = "Special Distribution Conditions")]
        public string? DistributionConditions { get; set; }

        // ── Workflow Metadata ───────────────────────────────────────
        [MaxLength(1000)]
        [Display(Name = "Open Deviations / Exceptions")]
        public string? OpenDeviationsNotes { get; set; }

        [MaxLength(2000)]
        [Display(Name = "Limitations / Conditions on Release")]
        public string? LimitationsNotes { get; set; }

        [MaxLength(2000)]
        [Display(Name = "General Notes")]
        public string? Notes { get; set; }

        // ── Hold ────────────────────────────────────────────────────
        [MaxLength(1000)]
        [Display(Name = "Hold Reason")]
        public string? HoldReason { get; set; }

        public DateTime? HoldDate { get; set; }

        [MaxLength(200)]
        public string? HoldBy { get; set; }

        // ── AP Decision ─────────────────────────────────────────────
        [MaxLength(500)]
        [Display(Name = "AP Certification Statement Override")]
        public string? APCertificationStatement { get; set; }

        public DateTime? CertificationDate { get; set; }

        [MaxLength(200)]
        public string? CertifiedBy { get; set; }

        [MaxLength(300)]
        [Display(Name = "AP Full Name & Qualification")]
        public string? APFullNameQualification { get; set; }

        [MaxLength(200)]
        [Display(Name = "Release Reference Number")]
        public string? ReleaseRegisterRef { get; set; }

        // ── Rejection ───────────────────────────────────────────────
        [MaxLength(1000)]
        public string? RejectionReason { get; set; }

        public DateTime? RejectionDate { get; set; }

        [MaxLength(200)]
        public string? RejectedBy { get; set; }

        // ── Audit ───────────────────────────────────────────────────
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        [MaxLength(200)]
        public string? LastModifiedBy { get; set; }

        public bool IsLocked { get; set; } = false;

        // ── Navigation ──────────────────────────────────────────────
        public ICollection<ReleaseChecklistItem>? ChecklistItems { get; set; }
        public ICollection<ReleaseTestResult>? TestResults { get; set; }
        public ICollection<ReleaseDocument>? Documents { get; set; }
        public ICollection<ReleaseCertificate>? Certificates { get; set; }
        public ICollection<ReleaseAuditEntry>? AuditEntries { get; set; }
        public ICollection<ReleaseCrossReference>? CrossReferences { get; set; }
        public ICollection<ReleaseTestingLab>? TestingLabs { get; set; }
    }
}
