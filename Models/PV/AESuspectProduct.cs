using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AESuspectProduct
    {
        public int Id { get; set; }

        public int AdverseEventId { get; set; }

        [ForeignKey("AdverseEventId")]
        public virtual AdverseEvent? AdverseEvent { get; set; }

        // Optional link to Product master
        public int? ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Display(Name = "Product Name")]
        public string? ProductName { get; set; }

        [Display(Name = "ARTG Number")]
        public string? ARTGNumber { get; set; }

        [Display(Name = "Batch / Lot Number")]
        public string? BatchNumber { get; set; }

        [Display(Name = "Route of Administration")]
        public string? RouteOfAdministration { get; set; }

        [Display(Name = "Dose")]
        public string? Dose { get; set; }

        [Display(Name = "Dose Unit")]
        public string? DoseUnit { get; set; }

        [Display(Name = "Frequency")]
        public string? DoseFrequency { get; set; }

        [Display(Name = "Indication")]
        public string? Indication { get; set; }

        [Display(Name = "Treatment Start Date")]
        public DateTime? TreatmentStartDate { get; set; }

        [Display(Name = "Treatment End Date")]
        public DateTime? TreatmentEndDate { get; set; }

        [Display(Name = "Action Taken")]
        public AEActionTaken? ActionTaken { get; set; }

        [Display(Name = "Dechallenge Result")]
        public AEDechallengeResult? DechallengeResult { get; set; }

        [Display(Name = "Rechallenge Result")]
        public AEDechallengeResult? RechallengeResult { get; set; }

        public enum AEActionTaken
        {
            [Display(Name = "Dose not changed")]
            DoseNotChanged,
            [Display(Name = "Dose reduced")]
            DoseReduced,
            [Display(Name = "Drug withdrawn temporarily")]
            WithdrawnTemporarily,
            [Display(Name = "Drug withdrawn permanently")]
            WithdrawnPermanently,
            [Display(Name = "Not applicable")]
            NotApplicable,
            Unknown
        }

        public enum AEDechallengeResult
        {
            Resolved,
            Resolving,
            Unchanged,
            [Display(Name = "Not applicable")]
            NotApplicable,
            Unknown
        }
    }
}
