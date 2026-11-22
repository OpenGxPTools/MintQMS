using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    /// <summary>
    /// The person named as responsible for one step (workflow stage) of a record. Information for accountability, never
    /// a permission: anyone allowed to sign the step still can. A row for the record's current stage is ACTIVE (StartedOn
    /// set); a row for a stage still ahead is PLANNED (StartedOn null) and becomes active — with its e-mail — when the
    /// record reaches that stage. Rows are never deleted; an assignment ends (EndedOn/EndReason) when the step is done,
    /// it is reassigned or unassigned, the planned person can no longer do the step, the record closes, or the user is
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
        /// <summary>Optional due date (a date, no time). For a planned step it is set when the step starts, from DueDays.</summary>
        public DateTime? DueDate { get; set; }
        /// <summary>Planned steps: due this many days after the step starts (optional).</summary>
        public int? DueDays { get; set; }
        /// <summary>When the record reached this stage with this person named (null while the step is only planned).</summary>
        public DateTime? StartedOn { get; set; }

        public bool IsPlanned => StartedOn == null;
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
        /// <summary>When the planned step started, the person could no longer do it (role, account or four-eyes rule).</summary>
        public const string NotAllowed = "NotAllowed";
        /// <summary>The record finished (or left the route) with this step still planned.</summary>
        public const string RecordClosed = "RecordClosed";
    }
}
