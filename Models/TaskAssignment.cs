using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    /// <summary>
    /// The person named as responsible for the next step of a record at one workflow stage. Information for
    /// accountability, never a permission: anyone allowed to sign the step still can. Rows are never deleted; an
    /// assignment ends (EndedOn/EndReason) when the record moves on, it is reassigned or unassigned, or the user is
    /// deactivated. Module is the workflow-rules module name (the controller, e.g. "Deviations").
    /// </summary>
    public class TaskAssignment
    {
        public int Id { get; set; }

        [MaxLength(40)]
        public string Module { get; set; } = string.Empty;
        public int RecordId { get; set; }
        /// <summary>The workflow state (stored code) the assignment belongs to.</summary>
        public int Stage { get; set; }

        public int AssignedToUserId { get; set; }
        public AppUser? AssignedToUser { get; set; }
        [MaxLength(256)]
        public string AssignedBy { get; set; } = string.Empty;
        public DateTime AssignedOn { get; set; }
        /// <summary>Optional due date (a date, no time).</summary>
        public DateTime? DueDate { get; set; }
        [MaxLength(500)]
        public string? Note { get; set; }

        public bool IsCurrent { get; set; } = true;
        public DateTime? EndedOn { get; set; }
        [MaxLength(20)]
        public string? EndReason { get; set; }
        /// <summary>Who ended it: the person who signed the step that moved the record on, reassigned, unassigned or
        /// deactivated the user.</summary>
        [MaxLength(256)]
        public string? EndedBy { get; set; }
    }

    public static class TaskAssignmentEndReasons
    {
        public const string MovedOn = "MovedOn";
        public const string Reassigned = "Reassigned";
        public const string Unassigned = "Unassigned";
        public const string UserDeactivated = "UserDeactivated";
    }
}
