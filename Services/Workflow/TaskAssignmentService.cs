using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Services.Email;

namespace OpenQMS.Services.Workflow
{
    /// <summary>
    /// Task assignment: who is responsible for each step of a record. The step the record is at now has at most one
    /// ACTIVE assignment; the steps still ahead can be PLANNED, and a planned person takes over (and is e-mailed) when the
    /// record reaches their step. Information for accountability, never a permission — the workflow steps keep their own
    /// checks, and anyone allowed may still sign. Who can be named comes from workflow-rules.json (roles of the stage's
    /// tasks) and the four-eyes rule. Every change is written to the record's own audit trail (in English, like every
    /// audit entry).
    /// </summary>
    public class TaskAssignmentService
    {
        // Messages shown after an assignment action (translated where shown; listed so tools/i18n.py collects them).
        internal static readonly string[] Messages = new[] { /* i18n */
            "The selected person cannot do this step of the record (role, account or four-eyes rule).",
            "The note can have at most 500 characters.",
            "The due date cannot be in the past.",
            "The number of days must be between 1 and 365.",
            "The responsibilities have been saved.",
            "Nothing was changed.",
            "{0} is named for “{1}” and for “{2}”: the four-eyes rule needs a different person for “{2}”.",
            "{0} raised this record and is named for “{1}” and for “{2}”: they may do “{2}” only if someone else does “{1}”.",
            "The person planned for the next step can no longer do it (role, account or four-eyes rule): no one is named for it now.",
        };

        /// <summary>Separates a message key from its arguments in a page message ("key\u001farg1\u001farg2").</summary>
        public const char ArgSeparator = '\u001f';

        /// <summary>The earlier steps of the route whose person the four-eyes rule keeps out of <paramref name="later"/>.</summary>
        public static List<WorkflowRules.Step> MustDifferFrom(IReadOnlyList<WorkflowRules.Step> route, WorkflowRules.Step later) =>
            later.FourEyes == null ? new()
            : route.TakeWhile(s => s != later).Where(s => s.Records != null && later.FourEyes.NotBy.Contains(s.Records)).ToList();

        /// <summary>The earlier step of the route that must be done by someone else if the person who raised the record
        /// does <paramref name="later"/> (e.g. a deviation's raiser may approve only if someone else accepted it).</summary>
        public static WorkflowRules.Step? RaiserReviewStep(IReadOnlyList<WorkflowRules.Step> route, WorkflowRules.Step later) =>
            later.FourEyes?.RaiserReview is { } f ? route.TakeWhile(s => s != later).FirstOrDefault(s => s.Records == f) : null;

        public const string NotAllowedMessage = "The selected person cannot do this step of the record (role, account or four-eyes rule).";

        /// <summary>One line of the responsibilities form: a stage of the record's route and who is named for it.</summary>
        public sealed class PlanEntry
        {
            public int Stage { get; set; }
            /// <summary>0 = no one.</summary>
            public int UserId { get; set; }
            /// <summary>The step the record is at now: an optional due date.</summary>
            public DateTime? DueDate { get; set; }
            /// <summary>A step still ahead: optionally due this many days after it starts.</summary>
            public int? DueDays { get; set; }
            public string? Note { get; set; }
        }

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

        private static int IdOf(object record) => (int)record.GetType().GetProperty("Id")!.GetValue(record)!;

        /// <summary>The plain task name of a stage ("Evaluate"), or "" when the stage has no task.</summary>
        public static string TaskAt(string module, int stage, object record) =>
            WorkflowRules.TasksFrom(module, stage, record).FirstOrDefault()?.Task ?? "";

        /// <summary>The English display name of a state code of the record's status enum.</summary>
        public static Enum StageValue(object record, int stage)
        {
            var type = record.GetType().GetProperty("Status")!.PropertyType;
            type = Nullable.GetUnderlyingType(type) ?? type;
            return (Enum)Enum.ToObject(type, stage);
        }

        /// <summary>The current (active or planned) assignment of the record for this stage, or null.</summary>
        public Task<TaskAssignment?> CurrentAsync(string module, int recordId, int state) =>
            _db.TaskAssignments.Include(t => t.AssignedToUser)
               .Where(t => t.IsCurrent && t.Module == module && t.RecordId == recordId && t.Stage == state)
               .OrderByDescending(t => t.Id)
               .FirstOrDefaultAsync();

        /// <summary>The record's current assignments (the active one and the planned ones) by stage.</summary>
        public async Task<Dictionary<int, TaskAssignment>> PlanAsync(string module, int recordId) =>
            (await _db.TaskAssignments.Include(t => t.AssignedToUser)
                .Where(t => t.IsCurrent && t.Module == module && t.RecordId == recordId).ToListAsync())
            .GroupBy(t => t.Stage).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Id).First());

        /// <summary>A deactivated user is locked out "forever"; a lock-out after wrong passwords is short.</summary>
        public static bool IsActive(AppUser u) => u.LockoutEnd == null || u.LockoutEnd < DateTimeOffset.UtcNow.AddYears(1);

        public static string DisplayName(AppUser u) =>
            string.IsNullOrWhiteSpace($"{u.FirstName}{u.LastName}") ? u.Email ?? u.UserName ?? "" : $"{u.FirstName} {u.LastName}".Trim();

        public Task<bool> FourEyesOnPublicAsync() => FourEyesOnAsync();

        private async Task<bool> FourEyesOnAsync() =>
            (await _db.SystemSettings.FindAsync(1))?.EnforceFourEyes ?? true;

        /// <summary>
        /// Active users who may do at least one task of a stage of the record (default: its current stage): they hold one
        /// of the task's roles (or are the user the task names, e.g. a document's author) and, with the four-eyes rule on,
        /// are not excluded by it as the record stands now. <paramref name="alsoExclude"/> drops one more user (the person
        /// signing the step that leads to a step with a four-eyes rule).
        /// </summary>
        public async Task<List<AppUser>> EligibleAsync(string module, object record, int? state = null, AppUser? alsoExclude = null,
                                                       IReadOnlyCollection<string>? redone = null)
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
                                  && tasks.Any(t => CanDo(module, t, record, u, byRole, fourEyes, redone)))
                      .ToList();
        }

        /// <summary>The person signing a step is left out of the next step's list when that step has a four-eyes rule
        /// (they have just done the work it checks).</summary>
        public static AppUser? ExcludeForNext(string module, int nextStage, object record, AppUser? signer) =>
            WorkflowRules.TasksFrom(module, nextStage, record).Any(t => t.FourEyes != null) ? signer : null;

        private static bool CanDo(string module, WorkflowRules.Step t, object record, AppUser u,
                                  Dictionary<string, HashSet<int>> byRole, bool fourEyes, IReadOnlyCollection<string>? redone)
        {
            var hasRole = t.Roles.Any(r => byRole.TryGetValue(r, out var ids) && ids.Contains(u.Id))
                          || (t.AlsoUserIn is { } f && FourEyes.IsUser(FourEyes.Value(record, f), u));
            return hasRole && (!fourEyes || (redone == null ? FourEyes.ExcludedReason(module, t, record, u)
                                                            : FourEyes.ExcludedForPlan(module, t, record, u, redone)) == null);
        }

        /// <summary>The people who can be PLANNED for a later step of the route: as EligibleAsync, but who did a step
        /// that will be done again before it (after a send-back) no longer counts.</summary>
        public Task<List<AppUser>> EligibleForPlanAsync(string module, object record, IReadOnlyList<WorkflowRules.Step> route, int stage) =>
            EligibleAsync(module, record, stage, null,
                          route.TakeWhile(s => s.From != stage).Select(s => s.Records).OfType<string>().ToHashSet());

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
        /// Who may assign, unassign and plan: anyone who may do a task of the current stage, and Administrators, Quality
        /// Managers and Regulatory Managers (who distribute work). ReadOnly users never.
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

        // ── Assign the step the record is at now ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Names <paramref name="assigneeId"/> as responsible for the step the record is at now (replacing any current
        /// assignment). Returns null, or the English reason it was not done (a translations.json key).
        /// </summary>
        public async Task<string?> AssignAsync(string module, object record, int assigneeId, DateTime? dueDate,
                                               string? note, string assignedBy, AppUser? alsoExclude = null)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();   // assignment and audit line together
            var why = await AssignNowAsync(module, record, assigneeId, dueDate, note, assignedBy, alsoExclude,
                                           AssignmentEmail.Kind.Assigned, checkDue: true);
            if (why != null)
                return why;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        private async Task<string?> AssignNowAsync(string module, object record, int assigneeId, DateTime? dueDate, string? note,
                                                   string assignedBy, AppUser? alsoExclude, AssignmentEmail.Kind kind,
                                                   bool checkDue, int? dueDays = null, string? auditAction = null)
        {
            var recordId = IdOf(record);
            var state = StateOf(record);
            var assignee = (await EligibleAsync(module, record, state, alsoExclude)).FirstOrDefault(u => u.Id == assigneeId);
            if (assignee == null)
                return NotAllowedMessage;
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note?.Length > 500)
                return "The note can have at most 500 characters.";
            if (checkDue && dueDate?.Date < SiteTime.Today)
                return "The due date cannot be in the past.";

            var now = DateTime.UtcNow;
            var current = await CurrentAsync(module, recordId, state);
            if (current != null)
                End(current, TaskAssignmentEndReasons.Reassigned, assignedBy, now);
            _db.TaskAssignments.Add(new TaskAssignment
            {
                Module = module, RecordId = recordId, Stage = state, AssignedToUserId = assignee.Id,
                AssignedBy = assignedBy, AssignedOn = now, StartedOn = now, DueDate = dueDate?.Date, DueDays = dueDays, Note = note,
            });
            var task = TaskAt(module, state, record);
            AddAudit(module, recordId, record, auditAction ?? (current == null ? "Task Assigned" : "Task Reassigned"),
                $"Responsible for \"{task}\": {DisplayName(assignee)} ({assignee.Email})"
                + (current?.AssignedToUser is { } prev && prev.Id != assignee.Id ? $", instead of {DisplayName(prev)} ({prev.Email})" : "")
                + (dueDate.HasValue ? $". Due {dueDate:yyyy-MM-dd}" : "") + (note != null ? $". Note: {note}" : "") + ".",
                assignedBy, now);
            await QueueEmailAsync(module, record, state, assignee, assignedBy, assignedBy, dueDate, note,
                                  current != null && kind == AssignmentEmail.Kind.Assigned ? AssignmentEmail.Kind.Reassigned : kind);
            return null;
        }

        public async Task<bool> UnassignAsync(string module, object record, string by)
        {
            var recordId = IdOf(record);
            var state = StateOf(record);
            var current = await CurrentAsync(module, recordId, state);
            if (current == null)
                return false;
            var now = DateTime.UtcNow;
            await using var tx = await _db.Database.BeginTransactionAsync();
            End(current, TaskAssignmentEndReasons.Unassigned, by, now);
            AddAudit(module, recordId, record, "Task Unassigned",
                $"No longer responsible for \"{TaskAt(module, state, record)}\": {DisplayName(current.AssignedToUser!)} ({current.AssignedToUser!.Email}).", by, now);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }

        // ── Plan the whole route ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Saves who is responsible for each step of the record's route: the step it is at now (assigned at once, with its
        /// e-mail) and the steps still ahead (planned: no e-mail until the record reaches them). Lines that did not change
        /// are left alone. Returns (error, warnings): on an error nothing is saved.
        /// </summary>
        public async Task<(string? Error, List<string> Warnings, bool Changed)> SavePlanAsync(
            string module, object record, IEnumerable<PlanEntry> entries, string by)
        {
            var warnings = new List<string>();
            var recordId = IdOf(record);
            var state = StateOf(record);
            var route = WorkflowRules.Route(module, state, record);
            var byStage = entries.GroupBy(e => e.Stage).ToDictionary(g => g.Key, g => g.Last());
            var existing = await PlanAsync(module, recordId);
            var now = DateTime.UtcNow;
            var changed = false;

            // Four-eyes: refuse a plan that names one person for two steps the rule keeps apart (it would fail later).
            if (await FourEyesOnAsync())
            {
                int Who(int stage) => byStage.TryGetValue(stage, out var e) ? e.UserId
                                    : existing.TryGetValue(stage, out var c) ? c.AssignedToUserId : 0;
                var raiser = FourEyes.Value(record, WorkflowRules.Modules[module].RaisedBy);
                foreach (var later in route)
                {
                    var who = Who(later.From);
                    if (who == 0)
                        continue;
                    var name = (await _users.FindByIdAsync(who.ToString())) is { } u ? DisplayName(u) : who.ToString();
                    if (MustDifferFrom(route, later).FirstOrDefault(s => Who(s.From) == who) is { } clash)
                        return (string.Join(ArgSeparator, "{0} is named for “{1}” and for “{2}”: the four-eyes rule needs a different person for “{2}”.",
                                            name, clash.Task, later.Task), warnings, false);
                    if (RaiserReviewStep(route, later) is { } review && Who(review.From) == who
                        && await _users.FindByIdAsync(who.ToString()) is { } ru && FourEyes.IsUser(raiser, ru))
                        return (string.Join(ArgSeparator, "{0} raised this record and is named for “{1}” and for “{2}”: they may do “{2}” only if someone else does “{1}”.",
                                            name, review.Task, later.Task), warnings, false);
                }
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            foreach (var step in route)
            {
                if (!byStage.TryGetValue(step.From, out var e))
                    continue;
                existing.TryGetValue(step.From, out var cur);
                var note = string.IsNullOrWhiteSpace(e.Note) ? null : e.Note.Trim();
                if (note?.Length > 500)
                    return ("The note can have at most 500 characters.", warnings, false);

                if (step.From == state)
                {
                    var same = cur != null && cur.AssignedToUserId == e.UserId && cur.DueDate == e.DueDate?.Date && cur.Note == note;
                    if (same || (cur == null && e.UserId == 0))
                        continue;
                    if (e.UserId == 0)
                    {
                        End(cur!, TaskAssignmentEndReasons.Unassigned, by, now);
                        AddAudit(module, recordId, record, "Task Unassigned",
                            $"No longer responsible for \"{step.Task}\": {DisplayName(cur!.AssignedToUser!)} ({cur.AssignedToUser!.Email}).", by, now);
                    }
                    else
                    {
                        // a due date already saved may stay, even if it has passed; a new one may not be in the past
                        var why = await AssignNowAsync(module, record, e.UserId, e.DueDate, note, by, null,
                                                       AssignmentEmail.Kind.Assigned, checkDue: cur?.DueDate != e.DueDate?.Date);
                        if (why != null)
                            return (why, warnings, false);
                    }
                    changed = true;
                    continue;
                }

                // a step still ahead
                if (e.DueDays is { } d && (d < 1 || d > 365))
                    return ("The number of days must be between 1 and 365.", warnings, false);
                if (cur != null && cur.AssignedToUserId == e.UserId && cur.DueDays == e.DueDays && cur.Note == note)
                    continue;
                if (cur == null && e.UserId == 0)
                    continue;
                AppUser? person = null;
                if (e.UserId != 0)
                {
                    person = (await EligibleForPlanAsync(module, record, route, step.From)).FirstOrDefault(u => u.Id == e.UserId);
                    if (person == null)
                        return (NotAllowedMessage, warnings, false);
                }
                if (cur != null)
                    End(cur, e.UserId == 0 ? TaskAssignmentEndReasons.Unassigned : TaskAssignmentEndReasons.Reassigned, by, now);
                if (person != null)
                {
                    _db.TaskAssignments.Add(new TaskAssignment
                    {
                        Module = module, RecordId = recordId, Stage = step.From, AssignedToUserId = person.Id,
                        AssignedBy = by, AssignedOn = now, DueDays = e.DueDays, Note = note,
                    });
                    AddAudit(module, recordId, record, cur == null ? "Step Planned" : "Planned Step Changed",
                        $"Planned for \"{step.Task}\": {DisplayName(person)} ({person.Email})"
                        + (cur?.AssignedToUser is { } prev && prev.Id != person.Id ? $", instead of {DisplayName(prev)} ({prev.Email})" : "")
                        + (e.DueDays is { } dd ? $". Due {dd} day(s) after the step starts" : "") + (note != null ? $". Note: {note}" : "") + ".",
                        by, now);
                }
                else
                {
                    AddAudit(module, recordId, record, "Planned Step Removed",
                        $"No longer planned for \"{step.Task}\": {DisplayName(cur!.AssignedToUser!)} ({cur.AssignedToUser!.Email}).", by, now);
                }
                changed = true;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (null, warnings, changed);
        }

        // ── The record moved to another stage ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called once a step has moved the record from <paramref name="from"/> to its current state (any route: forward,
        /// sent back, re-opened, cancelled). The assignment of the old stage ends — or, when the record was sent back and
        /// that step is ahead of it again, goes back to being planned; planned steps that are no longer on the route end;
        /// and the new stage gets its person: the one chosen in the signature dialog, else the one planned for it, else
        /// (for a step done before) the person who did it last time. Returns a warning (English key) or null.
        /// </summary>
        public async Task<string?> StageMovedAsync(string module, object record, int from, string by, AppUser? signer, int? chosen)
        {
            var recordId = IdOf(record);
            var to = StateOf(record);
            var now = DateTime.UtcNow;
            var route = WorkflowRules.Route(module, to, record);
            var ahead = route.Select(s => s.From).ToHashSet();
            var rows = await _db.TaskAssignments.Include(t => t.AssignedToUser)
                .Where(t => t.IsCurrent && t.Module == module && t.RecordId == recordId).ToListAsync();
            string? warning = null;

            await using var tx = await _db.Database.BeginTransactionAsync();
            foreach (var r in rows.Where(r => r.Stage != to))
            {
                if (!r.IsPlanned && r.Stage == from && ahead.Contains(r.Stage))
                {
                    // sent back: the person stays responsible for this step when the record comes back to it
                    r.StartedOn = null;
                    r.DueDate = null;
                    AddAudit(module, recordId, record, "Task Back to Planned",
                        $"\"{TaskAt(module, r.Stage, record)}\" is planned again for {DisplayName(r.AssignedToUser!)} ({r.AssignedToUser!.Email}): the record was sent back.", by, now);
                }
                else if (!r.IsPlanned)
                    End(r, TaskAssignmentEndReasons.MovedOn, by, now);
                else if (!ahead.Contains(r.Stage))
                {
                    End(r, TaskAssignmentEndReasons.RecordClosed, by, now);
                    AddAudit(module, recordId, record, "Planned Step Removed",
                        $"No longer planned for \"{WorkflowRules.Modules[module].Steps.FirstOrDefault(s => s.IsTask && s.From == r.Stage)?.Task}\": "
                        + $"{DisplayName(r.AssignedToUser!)} ({r.AssignedToUser!.Email}) — the record is no longer heading to that step.", by, now);
                }
            }

            if (route.Count > 0)
            {
                var plan = rows.FirstOrDefault(r => r.Stage == to && r.IsCurrent && r.IsPlanned);
                var active = rows.FirstOrDefault(r => r.Stage == to && r.IsCurrent && !r.IsPlanned);
                var exclude = ExcludeForNext(module, to, record, signer);
                if (chosen is > 0 && (plan == null || plan.AssignedToUserId != chosen))
                {
                    if (plan != null)
                        End(plan, TaskAssignmentEndReasons.Reassigned, by, now);
                    warning = await AssignNowAsync(module, record, chosen.Value, null, null, by, exclude, AssignmentEmail.Kind.Assigned, checkDue: false);
                }
                else if (plan != null)
                    warning = await StartPlannedAsync(module, record, plan, by, exclude, now);
                else if (active == null && await PreviousDoerAsync(module, record, to) is { } before
                         && (await EligibleAsync(module, record, to, exclude)).Any(u => u.Id == before.Id))
                    await AssignNowAsync(module, record, before.Id, null, null, by, exclude, AssignmentEmail.Kind.Returned,
                                         checkDue: false, auditAction: "Task Assigned (step done before)");
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return warning;
        }

        // The planned person takes over the step now, if they still can.
        private async Task<string?> StartPlannedAsync(string module, object record, TaskAssignment plan, string by, AppUser? exclude, DateTime now)
        {
            var recordId = IdOf(record);
            var task = TaskAt(module, plan.Stage, record);
            var person = plan.AssignedToUser!;
            if (!(await EligibleAsync(module, record, plan.Stage, exclude)).Any(u => u.Id == person.Id))
            {
                End(plan, TaskAssignmentEndReasons.NotAllowed, by, now);
                AddAudit(module, recordId, record, "Planned Step Not Started",
                    $"{DisplayName(person)} ({person.Email}) was planned for \"{task}\" but cannot do it (role, account or four-eyes rule); no one is named.", by, now);
                return "The person planned for the next step can no longer do it (role, account or four-eyes rule): no one is named for it now.";
            }
            plan.StartedOn = now;
            if (plan.DueDays is { } d)
                plan.DueDate = SiteTime.Today.AddDays(d);
            AddAudit(module, recordId, record, "Task Started",
                $"Responsible for \"{task}\": {DisplayName(person)} ({person.Email}), as planned"
                + (plan.DueDate.HasValue ? $". Due {plan.DueDate:yyyy-MM-dd}" : "") + ".", by, now);
            await QueueEmailAsync(module, record, plan.Stage, person, plan.AssignedBy, by, plan.DueDate, plan.Note, AssignmentEmail.Kind.Started);
            return null;
        }

        /// <summary>The person who last did this stage's forward step (from the record's audit trail), or null.</summary>
        private async Task<AppUser?> PreviousDoerAsync(string module, object record, int stage)
        {
            var step = WorkflowRules.TasksFrom(module, stage, record).FirstOrDefault();
            if (step == null)
                return null;
            var m = WorkflowRules.Modules[module];
            try
            {
                var who = (await _db.Database.SqlQueryRaw<string>(
                        $"SELECT \"PerformedBy\" AS \"Value\" FROM \"{m.AuditTable}\" WHERE \"{m.AuditKey}\" = {{0}} AND \"FromStatus\" = {{1}} AND \"ToStatus\" = {{2}}",
                        IdOf(record), StageValue(record, stage).ToString(), StageValue(record, step.To).ToString())
                    .ToListAsync()).LastOrDefault();
                if (string.IsNullOrWhiteSpace(who))
                    return null;
                return await _users.FindByNameAsync(who) ?? await _users.FindByEmailAsync(who);
            }
            catch (Exception)
            {
                return null;   // an audit table of another shape: no automatic assignment
            }
        }

        /// <summary>Queues the "task for you" e-mail (same save as the assignment) when System Settings → Send Assignment
        /// E-mails is on and the person has an address; not when people name themselves or take a step they just signed into.</summary>
        private async Task QueueEmailAsync(string module, object record, int stage, AppUser assignee, string assignedBy, string actor,
                                           DateTime? dueDate, string? note, AssignmentEmail.Kind kind)
        {
            var settings = await _db.SystemSettings.FindAsync(1);
            bool IsMe(string who) => string.Equals(assignee.UserName, who, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(assignee.Email, who, StringComparison.OrdinalIgnoreCase);
            if (settings?.SendAssignmentEmails != true || string.IsNullOrWhiteSpace(assignee.Email) || IsMe(actor))
                return;
            var assigner = await _users.FindByNameAsync(assignedBy) ?? await _users.FindByEmailAsync(assignedBy);
            _db.EmailOutbox.Add(AssignmentEmail.Compose(module, record, stage, assignee,
                assigner != null ? DisplayName(assigner) : assignedBy, dueDate, note, kind, settings));
        }

        /// <summary>A deactivated user's current and planned tasks become unassigned, each noted in its record's audit trail.</summary>
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
                if (IsWorkflowModule(t.Module) && await LoadRecordAsync(t.Module, t.RecordId) is { } record
                    && (StateOf(record) == t.Stage || t.IsPlanned))
                    AddAudit(t.Module, t.RecordId, record, t.IsPlanned ? "Planned Step Removed" : "Task Unassigned",
                        $"No longer {(t.IsPlanned ? "planned" : "responsible")} for \"{TaskAt(t.Module, t.Stage, record)}\": "
                        + $"{DisplayName(user)} ({user.Email}) — user account deactivated.", by, now);
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
