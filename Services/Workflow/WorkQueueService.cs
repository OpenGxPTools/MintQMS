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
        /// <summary>The step(s) that can be done next (English labels, translated on screen).</summary>
        public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
        public string? RaisedBy { get; init; }
        public DateTime? RaisedOn { get; init; }
        /// <summary>The current assignment for this stage, or null (with AssignedToUser loaded).</summary>
        public TaskAssignment? Assignment { get; init; }
        public object Record { get; init; } = default!;

        public bool IsOverdue(DateTime today) => Assignment?.DueDate is { } d && d.Date < today;
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
                    items.Add(new WorkItem
                    {
                        Module = m.Name,
                        RecordId = Id(record),
                        Number = Text(record, m.NumberField),
                        Title = Text(record, m.TitleField),
                        Stage = state,
                        StageValue = (Enum)record.GetType().GetProperty("Status")!.GetValue(record)!,
                        Steps = tasks.Select(t => t.Label).Distinct().ToList(),
                        RaisedBy = FourEyes.Value(record, m.RaisedBy),
                        RaisedOn = record.GetType().GetProperty(m.RaisedOnField)?.GetValue(record) as DateTime?,
                        Assignment = a,
                        Record = record,
                    });
                }
            }
            return items;
        }

        /// <summary>Waiting records assigned to this user.</summary>
        public static List<WorkItem> AssignedTo(IEnumerable<WorkItem> items, AppUser user) =>
            items.Where(i => i.Assignment?.AssignedToUserId == user.Id)
                 .OrderBy(i => i.Assignment!.DueDate ?? DateTime.MaxValue).ThenBy(i => i.Assignment!.AssignedOn).ToList();

        /// <summary>Waiting records that no one is assigned to and that this user may do (role or named user, active,
        /// not ReadOnly, not excluded by the four-eyes rule), oldest first.</summary>
        public async Task<List<WorkItem>> WaitingForAsync(IEnumerable<WorkItem> items, AppUser user)
        {
            var canDo = await _assign.UserCanDoAsync(user);
            return items.Where(i => i.Assignment == null && canDo(i.Module, i.Record))
                        .OrderBy(i => i.RaisedOn ?? DateTime.MaxValue).ToList();
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
