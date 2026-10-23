using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.LC
{
    public enum LCStatus
    {
        Draft            = 1,   // Record created; application not yet submitted
        Applied          = 2,   // Application submitted to authority/cert body
        [Display(Name = "Under Assessment")]
        UnderAssessment  = 3,   // Authority/cert body reviewing
        [Display(Name = "Issued / Active")]
        IssuedActive     = 4,   // Licence/cert granted and currently valid
        [Display(Name = "Conditions Applied")]
        ConditionsApplied= 5,   // Active but with post-issue conditions outstanding
        [Display(Name = "Renewal Due")]
        RenewalDue       = 6,   // Approaching expiry; renewal application should progress
        Expired          = 7,   // ExpiryDate passed; IsLocked=true
        Suspended        = 8,   // Authority suspended; IsLocked=true
        Withdrawn        = 9,   // Sponsor voluntarily surrendered; IsLocked=true
        [Display(Name = "Not Applicable")]
        NotApplicable    = 10,  // Confirmed not required; with documented justification
        [Display(Name = "Variation Required")]
        VariationRequired= 11   // Post-issue change in progress; record unlocked for editing
    }

    public enum LCCategory
    {
        RegulatoryLicence        = 1,   // Federal & state regulatory licences/permits
        ISOPrivateCertification  = 2,   // ISO / private quality certifications
        ThirdPartySiteApproval   = 3    // GMP clearances and third-party site approvals
    }

    public enum LCHolderType
    {
        OwnCompany  = 1,    // Held directly by the virtual sponsor entity
        ThirdParty  = 2     // Approvals for external manufacturing/supply sites
    }

    public enum LCDocumentType
    {
        Certificate             = 1,
        InspectionReport        = 2,
        GMPEvidencePackage      = 3,
        AuthorityLetter         = 4,
        ApplicationForm         = 5,
        SiteMasterFile          = 6,
        QualityAgreement        = 7,
        SurveillanceAuditReport = 8,
        Other                   = 9
    }

    public enum LCGmcPathway
    {
        MRA          = 1,   // Mutual Recognition Agreement country
        CV           = 2,   // Compliance Verification (recognised authority inspection)
        TGAInspection= 3    // TGA On-Site Inspection (no MRA/CV available)
    }

    public enum LCConditionStatus
    {
        Active    = 1,
        Completed = 2,
        Waived    = 3,
        Closed    = 4
    }
}
