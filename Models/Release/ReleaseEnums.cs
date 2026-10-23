using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Release
{
    public enum ReleaseType
    {
        [Display(Name = "Release For Supply")]
        ReleaseForSupply = 1,
        [Display(Name = "Release Under Quarantine")]
        ReleaseUnderQuarantine = 2,
        [Display(Name = "Release For Further Processing")]
        ReleaseForFurtherProcessing = 3,
        [Display(Name = "Release To Customer")]
        ReleaseToCustomer = 4,
        [Display(Name = "Quality Oversight")]
        QualityOversight = 5
    }

    public enum ReleaseStatus
    {
        Draft = 1,
        [Display(Name = "Under Review")]
        UnderReview = 2,
        [Display(Name = "On Hold")]
        OnHold = 3,
        [Display(Name = "Pending AP Decision")]
        PendingAPDecision = 4,
        Certified = 5,
        Released = 6,
        Rejected = 7,
        Closed = 8,
        Cancelled = 9
    }

    public enum ChecklistItemStatus
    {
        [Display(Name = "Not Reviewed")]
        NotReviewed = 1,
        Satisfactory = 2,
        [Display(Name = "Satisfactory with Comment")]
        SatisfactoryWithComment = 3,
        [Display(Name = "Not Applicable")]
        NotApplicable = 4,
        Outstanding = 5
    }

    public enum TestResultStatus
    {
        Pass = 1,
        Fail = 2,
        NotApplicable = 3,
        Pending = 4
    }

    public enum CertificateType
    {
        CoA = 1,
        CoC = 2,
        Combined = 3
    }

    public enum CertificateStatus
    {
        Draft = 1,
        Issued = 2,
        Superseded = 3,
        Withdrawn = 4
    }
}
