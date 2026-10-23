using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.RI
{
    public enum RIApplicationStatus
    {
        Strategy          = 1,   // Initial record; strategy being developed
        Preparation       = 2,   // Dossier being assembled
        Submitted         = 3,   // Submitted to authority; clock started
        [Display(Name = "Under Review")]
        UnderReview       = 4,   // Authority actively reviewing
        [Display(Name = "Clock Stop")]
        ClockStop         = 5,   // LOI issued; clock paused
        Approved          = 6,   // Authorisation granted; IsLocked=true
        [Display(Name = "Conditions Active")]
        ConditionsActive  = 7,   // Auto from Approved when conditions exist
        [Display(Name = "Variation Required")]
        VariationRequired = 8,   // Post-approval change in progress
        [Display(Name = "Renewal Due")]
        RenewalDue        = 9,   // Approaching expiry; renewal initiated
        Rejected          = 10,  // Authority refused
        Withdrawn         = 11,  // Applicant voluntarily withdrew / decided not to renew
        Suspended         = 12   // Authority suspended a granted authorisation; IsLocked=true
    }

    public enum RIApplicationCategory
    {
        [Display(Name = "Full Registration")]
        FullRegistration = 1,
        Variation        = 2,
        [Display(Name = "Access Pathway")]
        AccessPathway    = 3,
        [Display(Name = "Clinical Trial")]
        ClinicalTrial    = 4,
        Generic          = 5,
        Biosimilar       = 6,
        Designation      = 7,
        Renewal          = 8,
        Notification     = 9
    }

    public enum RIMilestoneStatus
    {
        Pending    = 1,
        [Display(Name = "In Progress")]
        InProgress = 2,
        Completed  = 3,
        Overdue    = 4,
        Skipped    = 5
    }

    public enum RIConditionType
    {
        REMS             = 1,
        RMP              = 2,
        [Display(Name = "Confirmatory Study")]
        ConfirmatoryStudy = 3,
        [Display(Name = "Supply Report")]
        SupplyReport     = 4,
        [Display(Name = "Label Update")]
        LabelUpdate      = 5,
        [Display(Name = "PSUR Schedule")]
        PSURSchedule     = 6,
        [Display(Name = "Safety Study")]
        SafetyStudy      = 7,
        Other            = 8
    }

    public enum RIConditionStatus
    {
        Outstanding = 1,
        [Display(Name = "In Progress")]
        InProgress  = 2,
        Submitted   = 3,
        Closed      = 4,
        Waived      = 5
    }

    public enum RIVariationType
    {
        TypeIA           = 1,
        TypeIAIN         = 2,
        TypeIB           = 3,
        TypeII           = 4,
        Extension        = 5,
        [Display(Name = "Major Variation")]
        MajorVariation   = 6,
        [Display(Name = "Minor Variation")]
        MinorVariation   = 7,
        [Display(Name = "Admin Notification")]
        AdminNotification = 8,
        PAS              = 9,
        CBE30            = 10,
        sNDA_sBLA        = 11,
        SNDS             = 12,
        CMN              = 13,
        CMA              = 14,
        USR              = 15,
        Other            = 16
    }

    public enum RIAccessPathwayType
    {
        SAS_A               = 1,
        SAS_B               = 2,
        SAS_C               = 3,
        [Display(Name = "Authorised Prescriber")]
        AuthorisedPrescriber = 4,
        CTN                 = 5,
        CTA                 = 6,
        SAP                 = 7,
        SAR                 = 8,
        [Display(Name = "Expanded Access")]
        ExpandedAccess      = 9,
        [Display(Name = "Named Patient")]
        NamedPatient        = 10
    }

    public enum RIAccessPathwayStatus
    {
        Requested              = 1,
        [Display(Name = "Eligibility Assessment")]
        EligibilityAssessment  = 2,
        Submitted              = 3,
        Approved               = 4,
        Active                 = 5,
        [Display(Name = "Suspended Pending Renewal")]
        SuspendedPendingRenewal = 6,
        Closed                 = 7,
        Rejected               = 8
    }

    public enum RIReportStatus
    {
        Draft          = 1,
        [Display(Name = "Pending Review")]
        PendingReview  = 2,
        Submitted      = 3,
        Overdue        = 4
    }
}
