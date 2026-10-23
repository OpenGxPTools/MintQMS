using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    // Risk classification set at the Assessment stage (Change Control Management SOP, Appendix B).
    public enum ChangeRiskClassification
    {
        Minor,
        Major,
        Critical
    }

    // Change type set at the Assessment stage; drives the pre-implementation action catalogue
    // (Change Control Management SOP, Appendix C). Built-in reference data — changes to this list
    // are a software change under Change Control / CSV.
    public enum ChangeTypeCategory
    {
        [Display(Name = "Supplier / manufacturer / contract service")]
        Supplier,
        [Display(Name = "Material / component / specification")]
        Material,
        [Display(Name = "Product / formulation / registered detail")]
        Product,
        [Display(Name = "Process (manufacturing / packaging / cleaning)")]
        Process,
        [Display(Name = "Equipment / facility / utility")]
        Equipment,
        [Display(Name = "Analytical method / test")]
        AnalyticalMethod,
        [Display(Name = "Computerised system")]
        ComputerisedSystem,
        [Display(Name = "Document / procedural")]
        Document,
        [Display(Name = "Label / artwork / product information")]
        Label,
        [Display(Name = "Regulatory-driven")]
        Regulatory,
        [Display(Name = "Storage / distribution / cold-chain")]
        StorageDistribution,
        Other
    }

    // Whether the change is permanent, time-bound, or made under emergency conditions
    // (Change Control Management SOP, §6.9).
    public enum ChangeNature
    {
        Permanent,
        Temporary,
        Emergency
    }

    // Tracking status of an individual action-plan item (Change Control Management SOP, Appendix D).
    public enum ActionItemStatus
    {
        [Display(Name = "Not started")]
        NotStarted,
        [Display(Name = "In progress")]
        InProgress,
        Complete,
        [Display(Name = "Not applicable")]
        NotApplicable
    }
}
