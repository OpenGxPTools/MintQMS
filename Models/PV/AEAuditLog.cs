using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AEAuditLog
    {
        public int Id { get; set; }

        public int AdverseEventId { get; set; }

        [ForeignKey("AdverseEventId")]
        public virtual AdverseEvent? AdverseEvent { get; set; }

        // StatusTransition | FieldEdit | TGASubmission | FollowUpCreated
        public string Action { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public string? Notes { get; set; }

        [Display(Name = "Performed By")]
        public string PerformedBy { get; set; } = string.Empty;

        [Display(Name = "Performed On")]
        public DateTime PerformedOn { get; set; }
    }
}
