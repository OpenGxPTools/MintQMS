using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services.Workflow
{
    /// <summary>
    /// Task assignment: the person named as responsible for a record's next step at its current stage. Information
    /// for accountability, never a permission — the workflow steps keep their own checks, and anyone allowed may
    /// still sign. Who can be named comes from workflow-rules.json (roles of the stage's tasks) and the four-eyes
    /// rule. Assignment changes are written to the record's own audit trail (in English, like every audit entry).
    /// </summary>
    public class TaskAssignmentService
    {
        // Messages shown after an assignment action (translated where shown; listed so tools/i18n.py collects them).
        internal static readonly string[] Messages = new[] { /* i18n */
            "The selected person cannot do the next step of this record (role, account or four-eyes rule).",
            "The note can have at most 500 characters.",
            "The person responsible for the next step has been saved.",
            "No one is named for the next step now.",
            "No one was named for the next step.",
        };

        private readonly ApplicationDbContext _db;
        private readonly UserManager<AppUser> _users;

        public TaskAssignmentService(ApplicationDbContext db, UserManager<AppUser> users)
        {
            _db = db;
            _users = users;
        }

        public static bool IsWorkflowModule(string? module) => module != null && WorkflowRules.Modules.ContainsKey(module);

        public async Task<object?> LoadRecordAsync(string module, int id)
        {
            var type = typeof(AppUser).Assembly.GetType("OpenQMS.Models." + WorkflowRules.Modules[module].Entity)!;
            return await _db.FindAsync(type, id);
        }

        public static int StateOf(object record) => FourEyes.StateOf(record);

        /// <summary>The current assignment for the record's current stage, or null.</summary>
        public Task<TaskAssignment?> CurrentAsync(string module, int recordId, int state) =>
            _db.TaskAssignments.Include(t => t.AssignedToUser)
               .Where(t => t.IsCurrent && t.Module == module && t.RecordId == recordId && t.Stage == state)
               .OrderByDescending(t => t.Id)
               .FirstOrDefaultAsync();

        /// <summary>A deactivated user is locked out "forever"; a lock-out after wrong passwords is short.</summary>
        public static bool IsActive(AppUser u) => u.LockoutEnd == null || u.LockoutEnd < DateTimeOffset.UtcNow.AddYears(1);

        public static string DisplayName(AppUser u) =>
            string.IsNullOrWhiteSpace($"{u.FirstName}{u.LastName}") ? u.Email ?? u.UserName ?? "" : $"{u.FirstName} {u.LastName}".Trim();

        private async Task<bool> FourEyesOnAsync() =>
            (await _db.SystemSettings.FindAsync(1))?.EnforceFourEyes ?? true;

        /// <summary>
        /// Active users who may do at least one task of the record's current stage: they hold one of the task's roles
        /// (or are the user the task names, e.g. a document's author) and, with the four-eyes rule on, are not excluded
        /// by it. <paramref name="alsoExclude"/> drops one more user (the person signing the step that leads here).
        /// </summary>
        public async Task<List<AppUser>> EligibleAsync(string module, object record, int? state = null, AppUser? alsoExclude = null)
        {
            var tasks = WorkflowRules.TasksFrom(module, state ?? StateOf(record), record);
            if (tasks.Count == 0)
                return new List<AppUser>();

            var fourEyes = await FourEyesOnAsync();
            var byRole = new Dictionary<string, HashSet<int>>();
            foreach (var role in tasks.SelectMany(t => t.Roles).Distinct())
                byRole[role] = (await _users.GetUsersInRoleAsync(role)).Select(u => u.Id).ToHashSet();
            var readOnly = (await _users.GetUsersInRoleAsync(Constants.ReadOnlyRole)).Select(u => u.Id).ToHashSet();

            var all = await _users.Users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
            return all.Where(u => IsActive(u) && !readOnly.Contains(u.Id) && u.Id != alsoExclude?.Id
                                  && tasks.Any(t => CanDo(module, t, record, u, byRole, fourEyes)))
                      .ToList();
        }

        private static bool CanDo(string module, WorkflowRules.Step t, object record, AppUser u,
                                  Dictionary<string, HashSet<int>> byRole, bool fourEyes)
        {
            var hasRole = t.Roles.Any(r => byRole.TryGetValue(r, out var ids) && ids.Contains(u.Id))
                          || (t.AlsoUserIn is { } f && FourEyes.IsUser(FourEyes.Value(record, f), u));
            return hasRole && (!fourEyes || FourEyes.ExcludedReason(module, t, record, u) == null);
        }

        /// <summary>
        /// Whether this user may do a task of a record's current stage: holds one of the task's roles (or is the user the
        /// task names), is active and not ReadOnly, and — with the four-eyes rule on — is not excluded by it. One role
        /// look-up for many records (the work lists).
        /// </summary>
        public async Task<Func<string, object, bool>> UserCanDoAsync(AppUser user)
        {
            var roles = (await _users.GetRolesAsync(user)).ToHashSet();
            if (!IsActive(user) || roles.Contains(Constants.ReadOnlyRole))
                return (_, _) => false;
            var fourEyes = await FourEyesOnAsync();
            return (module, record) => WorkflowRules.TasksFrom(module, StateOf(record), record).Any(t =>
                (t.Roles.Any(roles.Contains) || (t.AlsoUserIn is { } f && FourEyes.IsUser(FourEyes.Value(record, f), user)))
                && (!fourEyes || FourEyes.ExcludedReason(module, t, record, user) == null));
        }

        /// <summary>Current assignments of a module by record (any stage; match the record's state when showing one).</summary>
        public async Task<Dictionary<int, List<TaskAssignment>>> CurrentByRecordAsync(string module) =>
            (await _db.TaskAssignments.AsNoTracking().Include(t => t.AssignedToUser)
                .Where(t => t.IsCurrent && t.Module == module).ToListAsync())
            .GroupBy(t => t.RecordId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Id).ToList());

        /// <summary>The assignment in <paramref name="map"/> for this record at this state, or null.</summary>
        public static TaskAssignment? For(Dictionary<int, List<TaskAssignment>> map, int recordId, int state) =>
            map.TryGetValue(recordId, out var list) ? list.FirstOrDefault(t => t.Stage == state) : null;

        /// <summary>
        /// Who may assign or unassign: anyone who may do a task of this stage, and Administrators, Quality Managers and
        /// Regulatory Managers (who distribute work). ReadOnly users never.
        /// </summary>
        public async Task<bool> CanAssignAsync(string module, object record, ClaimsPrincipal principal)
        {
            if (principal.IsInRole(Constants.ReadOnlyRole))
                return false;
            var tasks = WorkflowRules.TasksFrom(module, StateOf(record), record);
            if (tasks.Count == 0)
                return false;
            if (principal.IsInRole(Constants.AdministratorsRole) || principal.IsInRole(Constants.QualityManagerRole)
                || principal.IsInRole(Constants.RegulatoryManagerRole))
                return true;
            var user = await _users.GetUserAsync(principal);
            return user != null && tasks.Any(t => t.Roles.Any(principal.IsInRole)
                                                  || (t.AlsoUserIn is { } f && FourEyes.IsUser(FourEyes.Value(record, f), user)));
        }

        /// <summary>
        /// Names <paramref name="assigneeId"/> as responsible for the record's current stage (replacing any current
        /// assignment). Returns null, or the English reason it was not done (a translations.json key).
        /// </summary>
        public async Task<string?> AssignAsync(string module, object record, int assigneeId, DateTime? dueDate,
                                               string? note, string assignedBy, AppUser? alsoExclude = null)
        {
            var recordId = (int)record.GetType().GetProperty("Id")!.GetValue(record)!;
            var state = StateOf(record);
            var eligible = await EligibleAsync(module, record, state, alsoExclude);
            var assignee = eligible.FirstOrDefault(u => u.Id == assigneeId);
            if (assignee == null)
                return "The selected person cannot do the next step of this record (role, account or four-eyes rule).";
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note?.Length > 500)
                return "The note can have at most 500 characters.";

            var now = DateTime.UtcNow;
            await using var tx = await _db.Database.BeginTransactionAsync();   // assignment and audit line together
            var current = await CurrentAsync(module, recordId, state);
            if (current != null)
                End(current, TaskAssignmentEndReasons.Reassigned, assignedBy, now);
            _db.TaskAssignments.Add(new TaskAssignment
            {
                Module = module, RecordId = recordId, Stage = state, AssignedToUserId = assignee.Id,
                AssignedBy = assignedBy, AssignedOn = now, DueDate = dueDate?.Date, Note = note,
            });
            AddAudit(module, recordId, record, current == null ? "Task Assigned" : "Task Reassigned",
                $"Responsible for the next step: {DisplayName(assignee)} ({assignee.Email})"
                + (current?.AssignedToUser is { } prev ? $", instead of {DisplayName(prev)} ({prev.Email})" : "")
                + (dueDate.HasValue ? $". Due {dueDate:yyyy-MM-dd}" : "") + (note != null ? $". Note: {note}" : "") + ".",
                assignedBy, now);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        public async Task<bool> UnassignAsync(string module, object record, string by)
        {
            var recordId = (int)record.GetType().GetProperty("Id")!.GetValue(record)!;
            var current = await CurrentAsync(module, recordId, StateOf(record));
            if (current == null)
                return false;
            var now = DateTime.UtcNow;
            await using var tx = await _db.Database.BeginTransactionAsync();
            End(current, TaskAssignmentEndReasons.Unassigned, by, now);
            AddAudit(module, recordId, record, "Task Unassigned",
                $"No longer responsible for the next step: {DisplayName(current.AssignedToUser!)} ({current.AssignedToUser!.Email}).", by, now);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }

        /// <summary>Ends the record's current assignments that belong to another stage (the record has moved on).</summary>
        public async Task EndMovedOnAsync(string module, int recordId, int newState, string by)
        {
            var stale = await _db.TaskAssignments
                .Where(t => t.IsCurrent && t.Module == module && t.RecordId == recordId && t.Stage != newState)
                .ToListAsync();
            if (stale.Count == 0)
                return;
            var now = DateTime.UtcNow;
            foreach (var t in stale)
                End(t, TaskAssignmentEndReasons.MovedOn, by, now);
            await _db.SaveChangesAsync();
        }

        /// <summary>A deactivated user's current tasks become unassigned, each noted in its record's audit trail.</summary>
        public async Task EndForDeactivatedUserAsync(AppUser user, string by)
        {
            var open = await _db.TaskAssignments.Where(t => t.IsCurrent && t.AssignedToUserId == user.Id).ToListAsync();
            if (open.Count == 0)
                return;
            var now = DateTime.UtcNow;
            await using var tx = await _db.Database.BeginTransactionAsync();
            foreach (var t in open)
            {
                End(t, TaskAssignmentEndReasons.UserDeactivated, by, now);
                if (IsWorkflowModule(t.Module) && await LoadRecordAsync(t.Module, t.RecordId) is { } record && StateOf(record) == t.Stage)
                    AddAudit(t.Module, t.RecordId, record, "Task Unassigned",
                        $"No longer responsible for the next step: {DisplayName(user)} ({user.Email}) — user account deactivated.", by, now);
            }
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        private static void End(TaskAssignment t, string reason, string by, DateTime now)
        {
            t.IsCurrent = false;
            t.EndedOn = now;
            t.EndReason = reason;
            t.EndedBy = by;
        }

        // One line in the record's own audit trail. Table and column names come from the embedded rules file.
        private void AddAudit(string module, int recordId, object record, string action, string notes, string by, DateTime now)
        {
            var m = WorkflowRules.Modules[module];
            var state = record.GetType().GetProperty("Status")!.GetValue(record)!.ToString();
            _db.Database.ExecuteSqlRaw(
                $"INSERT INTO \"{m.AuditTable}\" (\"{m.AuditKey}\", \"Action\", \"FromStatus\", \"ToStatus\", \"Notes\", \"PerformedBy\", \"PerformedOn\") " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                recordId, action, state!, state!, notes, by, now);
        }
    }
}
