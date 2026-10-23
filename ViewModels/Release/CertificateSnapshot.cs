using OpenQMS.Models.Release;

namespace OpenQMS.ViewModels.Release
{
    public class CertificateSnapshot
    {
        public string RecordNumber { get; set; } = string.Empty;
        public string CertificateNumber { get; set; } = string.Empty;
        public CertificateType CertificateType { get; set; }
        public ReleaseType ReleaseType { get; set; }

        // Product
        public string ProductName { get; set; } = string.Empty;
        public OpenQMS.Models.ProductType? ProductType { get; set; }
        public string? BrandName { get; set; }
        public string? InnName { get; set; }
        public string? InciName { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? PresentationForm { get; set; }
        public string? ArtgReference { get; set; }
        public string? DeviceTypeCategory { get; set; }
        public string? ModelVersion { get; set; }
        public string? StandardScientificName { get; set; }
        public string? RawMaterialTypeCategory { get; set; }
        public string? Grade { get; set; }

        // Batch
        public string? BatchNumber { get; set; }
        public string? BatchSize { get; set; }
        public string? PackSize { get; set; }
        public DateTime? ManufacturingDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? RetestDate { get; set; }

        // Sites
        public string? ManufacturingSite { get; set; }
        public string? ManufacturingSiteGmpRef { get; set; }
        public string? PackagingSite { get; set; }
        public string? PackagingSiteGmpRef { get; set; }
        public List<TestingLabSnapshot> TestingLabs { get; set; } = new();
        // Legacy field kept for backward-compatible deserialization of old certificates
        public string? TestingLaboratory { get; set; }
        public string? ReleasingCompany { get; set; }
        public string? ReleasingCompanyLicenceRef { get; set; }
        public string? FinalReleaseParty { get; set; }
        public string? TechnicalAgreementRef { get; set; }

        // Release info
        public string? StorageConditions { get; set; }
        public string? OpenDeviationsNotes { get; set; }
        public string? LimitationsNotes { get; set; }

        // AP / issuance
        public string? APName { get; set; }
        public string? APCertificationStatement { get; set; }
        public DateTime? CertificationDate { get; set; }
        public DateTime IssuedDate { get; set; }
        public string? ReleaseRegisterRef { get; set; }
        public string? CertificateIssuanceStatement { get; set; }
        public string? IssuedByFullName { get; set; }

        public List<TestResultSnapshot> TestResults { get; set; } = new();
        public List<ChecklistItemSnapshot> ChecklistItems { get; set; } = new();
    }

    public class TestingLabSnapshot
    {
        public string LaboratoryName { get; set; } = string.Empty;
        public string? GmpLicenceRef { get; set; }
    }

    public class TestResultSnapshot
    {
        public string TestParameter { get; set; } = string.Empty;
        public string? TestMethod { get; set; }
        public string AcceptanceCriterion { get; set; } = string.Empty;
        public string? Result { get; set; }
        public TestResultStatus Status { get; set; }
    }

    public class ChecklistItemSnapshot
    {
        public string Category { get; set; } = string.Empty;
        public string ItemText { get; set; } = string.Empty;
        public ChecklistItemStatus Status { get; set; }
        public string? Comment { get; set; }
    }
}
