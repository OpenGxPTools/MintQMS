using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    // A single task in a change's detailed action plan (Change Control Management SOP, §6.4a / Appendix D).
    // Edited during Assessment and Implementation; edits are recorded in the ChangeAuditLog. Not an audit
    // table, so the append-only triggers do not apply — rows can be updated and deleted.
    public class ChangeActionPlanItem
    {
        public int Id { get; set; }

        public int ChangeId { get; set; }
        [ForeignKey("ChangeId")]
        public virtual Change? Change { get; set; }

        public int DisplayOrder { get; set; }

        [Display(Name = "Action / task")]
        public string Action { get; set; } = string.Empty;

        // Free text: a system user's name or an external owner.
        public string? Owner { get; set; }

        [Display(Name = "Target date")]
        public DateTime? TargetDate { get; set; }

        public ActionItemStatus Status { get; set; } = ActionItemStatus.NotStarted;

        [Display(Name = "Evidence / reference")]
        public string? EvidenceReference { get; set; }

        // Set only when Status is Complete (enforced in the controller). Used to gate progression
        // to approval: every applicable (non-N/A) item must be completed first.
        [Display(Name = "Completed date")]
        public DateTime? CompletedDate { get; set; }
    }
}
