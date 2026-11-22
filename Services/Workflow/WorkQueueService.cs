using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services.Workflow
{
    /// <summary>One record of a workflow module that is waiting for a step (its stage has a task).</summary>
    public sealed class WorkItem
    {
        public string Module { get; init; } = "";
        /// <summary>The module's name on screen (English key).</summary>
        public string ModuleName => WorkflowRules.Modules[Module].DisplayName;
        public int RecordId { get; init; }
        public string Number { get; init; } = "";
        public string Title { get; init; } = "";
        public int Stage { get; init; }
        /// <summary>The record's state, for display (L.Enum).</summary>
        public Enum StageValue { get; init; } = default!;
        /// <summary>The task(s) waiting at this stage, in plain words (English keys, translated on screen).</summary>
        public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
        /// <summary>The record's own target date (e.g. a deviation's target resolution date), if the module has one.</summary>
        public DateTime? TargetDate { get; init; }
        public string? RaisedBy { get; init; }
        public DateTime? RaisedOn { get; init; }
        /// <summary>The current assignment for this stage, or null (with AssignedToUser loaded).</summary>
        public TaskAssignment? Assignment { get; init; }
        /// <summary>People planned for the steps after this one, in route order (with AssignedToUser loaded).</summary>
        public IReadOnlyList<(string Task, TaskAssignment Plan)> PlannedAhead { get; init; } = Array.Empty<(string, TaskAssignment)>();
        /// <summary>Display name of whoever assigned or planned it.</summary>
        public string? AssignedByName { get; init; }
        public object Record { get; init; } = default!;

        /// <summary>The date to work to: the step's due date, else the record's target date.</summary>
        public DateTime? Due => Assignment?.DueDate ?? TargetDate;
        /// <summary>A planned step (Coming up): not ready yet; due this many days after it starts.</summary>
        public bool IsPlanned => Assignment?.IsPlanned == true;

        public bool IsOverdue(DateTime today) => !IsPlanned && Due is { } d && d.Date < today;
    }

    /// <summary>
    /// Work lists built from the workflow rules for the nine workflow modules: every record waiting for a step, with its
    /// current assignment; what is assigned to a person; what is waiting, unassigned, for the roles a person holds (four-
    /// eyes respected); and what a person raised that is still in progress. Read-only; used by the home page, the
    /// registers and the Task Assignments report.
    /// </summary>
    public class WorkQueueService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<AppUser> _users;
        private readonly TaskAssignmentService _assign;

        public WorkQueueService(ApplicationDbContext db, UserManager<AppUser> users, TaskAssignmentService assign)
        {
            _db = db;
            _users = users;
            _assign = assign;
        }

        /// <summary>Every record (not deleted) of the workflow modules whose current stage has a task.</summary>
        public async Task<List<WorkItem>> OpenItemsAsync(string? onlyModule = null)
        {
            var current = await _db.TaskAssignments.AsNoTracking().Include(t => t.AssignedToUser)
                .Where(t => t.IsCurrent && (onlyModule == null || t.Module == onlyModule)).ToListAsync();
            var byRecord = current.GroupBy(t => (t.Module, t.RecordId, t.Stage))
                                  .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Id).First());
            var names = await NamesAsync(current.Select(t => t.AssignedBy));

            var items = new List<WorkItem>();
            foreach (var m in WorkflowRules.Modules.Values.Where(m => onlyModule == null || m.Name == onlyModule))
            {
                foreach (var record in await LoadWaitingAsync(m))
                {
                    var state = TaskAssignmentService.StateOf(record);
                    var tasks = WorkflowRules.TasksFrom(m.Name, state, record);
                    if (tasks.Count == 0)
                        continue;   // e.g. supplier qualification when it is not required
                    byRecord.TryGetValue((m.Name, Id(record), state), out var a);
                    var ahead = new List<(string, TaskAssignment)>();
                    foreach (var step in WorkflowRules.Route(m.Name, state, record).Skip(1))
                        if (byRecord.TryGetValue((m.Name, Id(record), step.From), out var p) && p.IsPlanned)
                            ahead.Add((step.Task, p));
                    items.Add(new WorkItem
                    {
                        Module = m.Name,
                        RecordId = Id(record),
                        Number = Text(record, m.NumberField),
                        Title = Text(record, m.TitleField),
                        Stage = state,
                        StageValue = (Enum)record.GetType().GetProperty("Status")!.GetValue(record)!,
                        Steps = tasks.Select(t => t.Task).Distinct().ToList(),
                        TargetDate = Target(record, m),
                        RaisedBy = FourEyes.Value(record, m.RaisedBy),
                        RaisedOn = record.GetType().GetProperty(m.RaisedOnField)?.GetValue(record) as DateTime?,
                        Assignment = a,
                        AssignedByName = a != null && names.TryGetValue(a.AssignedBy, out var n) ? n : a?.AssignedBy,
                        PlannedAhead = ahead,
                        Record = record,
                    });
                }
            }
            return items;
        }

        /// <summary>Waiting records assigned to this user, the most urgent first.</summary>
        public static List<WorkItem> AssignedTo(IEnumerable<WorkItem> items, AppUser user) =>
            items.Where(i => i.Assignment?.AssignedToUserId == user.Id)
                 .OrderBy(i => i.Due ?? DateTime.MaxValue).ThenBy(i => i.Assignment!.AssignedOn).ToList();

        /// <summary>Steps planned for this user that the record has not reached yet (Coming up), oldest plan first.</summary>
        public async Task<List<WorkItem>> PlannedForAsync(AppUser user)
        {
            var plans = await _db.TaskAssignments.AsNoTracking().Include(t => t.AssignedToUser)
                .Where(t => t.IsCurrent && t.StartedOn == null && t.AssignedToUserId == user.Id)
                .OrderBy(t => t.AssignedOn).ToListAsync();
            var names = await NamesAsync(plans.Select(t => t.AssignedBy));
            var items = new List<WorkItem>();
            foreach (var t in plans)
            {
                if (!WorkflowRules.Modules.TryGetValue(t.Module, out var m) || await _assign.LoadRecordAsync(t.Module, t.RecordId) is not { } record
                    || record.GetType().GetProperty("IsDeleted")?.GetValue(record) is true)
                    continue;
                _db.Entry(record).State = EntityState.Detached;
                items.Add(new WorkItem
                {
                    Module = t.Module,
                    RecordId = t.RecordId,
                    Number = Text(record, m.NumberField),
                    Title = Text(record, m.TitleField),
                    Stage = t.Stage,
                    StageValue = (Enum)record.GetType().GetProperty("Status")!.GetValue(record)!,
                    Steps = new[] { TaskAssignmentService.TaskAt(t.Module, t.Stage, record) }.Where(x => x != "").ToList(),
                    RaisedBy = FourEyes.Value(record, m.RaisedBy),
                    RaisedOn = record.GetType().GetProperty(m.RaisedOnField)?.GetValue(record) as DateTime?,
                    TargetDate = Target(record, m),
                    Assignment = t,
                    AssignedByName = names.TryGetValue(t.AssignedBy, out var n) ? n : t.AssignedBy,
                    Record = record,
                });
            }
            return items;
        }

        // user name / e-mail → display name, for "assigned by" / "planned by"
        private async Task<Dictionary<string, string>> NamesAsync(IEnumerable<string> who)
        {
            var keys = who.Where(w => !string.IsNullOrEmpty(w)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var users = await _users.Users.AsNoTracking().Where(u => keys.Contains(u.UserName!) || keys.Contains(u.Email!)).ToListAsync();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in users)
            {
                if (u.UserName != null) map[u.UserName] = TaskAssignmentService.DisplayName(u);
                if (u.Email != null) map[u.Email] = TaskAssignmentService.DisplayName(u);
            }
            return map;
        }

        private static DateTime? Target(object record, WorkflowRules.Module m) =>
            m.TargetDateField is { } f ? record.GetType().GetProperty(f)?.GetValue(record) as DateTime? : null;

        /// <summary>Waiting records whose step this user may do (role or named user, active, not ReadOnly, not excluded by
        /// the four-eyes rule) and that are not assigned to them (those are in My Tasks) — whether no one or someone else
        /// is named: the earliest due date first, then the oldest.</summary>
        public async Task<List<WorkItem>> WaitingForAsync(IEnumerable<WorkItem> items, AppUser user)
        {
            var canDo = await _assign.UserCanDoAsync(user);
            return items.Where(i => i.Assignment?.AssignedToUserId != user.Id && canDo(i.Module, i.Record))
                        .OrderBy(i => i.Due ?? DateTime.MaxValue).ThenBy(i => i.RaisedOn ?? DateTime.MaxValue).ToList();
        }

        /// <summary>Waiting records this user raised (the module's "raised by" field), newest first.</summary>
        public static List<WorkItem> RaisedBy(IEnumerable<WorkItem> items, AppUser user) =>
            items.Where(i => FourEyes.IsUser(i.RaisedBy, user))
                 .OrderByDescending(i => i.RaisedOn ?? DateTime.MinValue).ToList();

        private static int Id(object record) => (int)record.GetType().GetProperty("Id")!.GetValue(record)!;

        private static string Text(object record, string field) =>
            record.GetType().GetProperty(field)?.GetValue(record)?.ToString() ?? "";

        // Records of the module in a state that has a task, not deleted, without tracking.
        private async Task<List<object>> LoadWaitingAsync(WorkflowRules.Module m)
        {
            var type = typeof(AppUser).Assembly.GetType("OpenQMS.Models." + m.Entity)!;
            var states = m.Steps.Where(s => s.IsTask).Select(s => s.From).Distinct().ToList();
            var load = typeof(WorkQueueService).GetMethod(nameof(LoadTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                                               .MakeGenericMethod(type);
            return await (Task<List<object>>)load.Invoke(this, new object[] { states })!;
        }

        private async Task<List<object>> LoadTypedAsync<T>(List<int> states) where T : class
        {
            var e = Expression.Parameter(typeof(T), "e");
            Expression body = Expression.Call(Expression.Constant(states), typeof(List<int>).GetMethod(nameof(List<int>.Contains))!,
                                              Expression.Convert(Expression.Property(e, "Status"), typeof(int)));
            if (typeof(T).GetProperty("IsDeleted") is { PropertyType: var t } && t == typeof(bool))
                body = Expression.AndAlso(body, Expression.Not(Expression.Property(e, "IsDeleted")));
            var rows = await _db.Set<T>().AsNoTracking().Where(Expression.Lambda<Func<T, bool>>(body, e)).ToListAsync();
            return rows.Cast<object>().ToList();
        }
    }
}
