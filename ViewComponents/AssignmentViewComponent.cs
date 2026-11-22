using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenQMS.Models;
using OpenQMS.Services;
using OpenQMS.Services.Workflow;

namespace OpenQMS.ViewComponents
{
    /// <summary>
    /// The "Responsibilities" box on a workflow record's page — who does the step the record is at now and who is
    /// planned for each step still ahead — and the list of people each of the page's signature dialogs can name for the
    /// step after it (the planned person chosen already). Usage in a Details view:
    /// <c>@await Component.InvokeAsync("Assignment", new { module = "Deviations", recordId = Model.Id })</c>
    /// </summary>
    public class AssignmentViewComponent : ViewComponent
    {
        private readonly TaskAssignmentService _svc;
        private readonly UserManager<AppUser> _users;

        public AssignmentViewComponent(TaskAssignmentService svc, UserManager<AppUser> users)
        {
            _svc = svc;
            _users = users;
        }

        public sealed record Person(int Id, string Name, string Email);

        /// <summary>One step of the record's route.</summary>
        public sealed class Row
        {
            public int Stage { get; init; }
            /// <summary>Plain task name (English key), e.g. "Evaluate".</summary>
            public string Task { get; init; } = "";
            /// <summary>The record status at that step (shown under the task).</summary>
            public Enum StageValue { get; init; } = default!;
            public bool IsNow { get; init; }
            public TaskAssignment? Assignment { get; init; }
            public List<Person> Eligible { get; init; } = new();
            /// <summary>Roles that can do the step (role names, translated on screen).</summary>
            public List<string> Roles { get; init; } = new();
            /// <summary>Earlier steps of the route whose person cannot do this one (four-eyes): (stage, task).</summary>
            public List<(int Stage, string Task)> NotSameAs { get; init; } = new();
            /// <summary>The raiser may do this step only if someone else does this earlier step (stage, task), else null.</summary>
            public (int Stage, string Task)? RaiserOnlyIfOtherAt { get; init; }
            /// <summary>People left out of the list because they already did an earlier step this one checks: (name, task).</summary>
            public List<(string Name, string Task)> AlreadyOut { get; init; } = new();
            /// <summary>A document step its author may do whatever their role.</summary>
            public bool AuthorToo { get; init; }
        }

        public sealed class Model
        {
            public string Module { get; init; } = "";
            public int RecordId { get; init; }
            public List<Row> Rows { get; init; } = new();
            public bool CanAssign { get; init; }
            public bool FourEyesOn { get; init; }
            /// <summary>The person who raised the record (for the raiser rule), or null.</summary>
            public int? RaiserId { get; init; }
            /// <summary>Open the responsibilities dialog when the page loads (link from the Task Assignments report).</summary>
            public bool OpenDialog { get; init; }
            /// <summary>Signature dialog form action ("/Deviations/AcceptDeviation") → who can be named for the step after it.</summary>
            public Dictionary<string, List<Person>> Next { get; init; } = new();
            /// <summary>Signature dialog form action → the person planned for the step after it (chosen already).</summary>
            public Dictionary<string, int> Planned { get; init; } = new();
            public string? MessageType { get; init; }
            public string? Message { get; init; }
        }

        public async Task<IViewComponentResult> InvokeAsync(string module, int recordId)
        {
            var record = await _svc.LoadRecordAsync(module, recordId);
            if (record == null)
                return Content("");
            var state = TaskAssignmentService.StateOf(record);
            var me = await _users.GetUserAsync(HttpContext.User);
            static Person P(AppUser u) => new(u.Id, TaskAssignmentService.DisplayName(u), u.Email ?? "");
            var plan = await _svc.PlanAsync(module, recordId);

            var next = new Dictionary<string, List<Person>>();
            var planned = new Dictionary<string, int>();
            if (me != null && !User.IsInRole(OpenQMS.Authorization.Constants.ReadOnlyRole))
            {
                foreach (var step in WorkflowRules.Modules[module].Steps.Where(s => s.From == state))
                {
                    var key = $"/{module}/{step.Action}";
                    if (WorkflowRules.TasksFrom(module, step.To, record).Count == 0 || next.ContainsKey(key))
                        continue;
                    // whoever signs this step has just done the work a four-eyes step after it checks
                    var exclude = TaskAssignmentService.ExcludeForNext(module, step.To, record, me);
                    next[key] = (await _svc.EligibleAsync(module, record, step.To, exclude)).Select(P).ToList();
                    if (plan.TryGetValue(step.To, out var p) && next[key].Any(x => x.Id == p.AssignedToUserId))
                        planned[key] = p.AssignedToUserId;
                }
            }

            var canAssign = await _svc.CanAssignAsync(module, record, HttpContext.User);
            var rows = new List<Row>();
            var route = WorkflowRules.Route(module, state, record);
            foreach (var step in route)
            {
                var review = TaskAssignmentService.RaiserReviewStep(route, step);
                var redone = route.TakeWhile(s => s.From != step.From).Select(s => s.Records).OfType<string>().ToHashSet();
                var alreadyOut = new List<(string, string)>();
                if (step.FourEyes != null)
                    foreach (var field in step.FourEyes.NotBy.Where(f => !redone.Contains(f)))
                    {
                        var who = record.GetType().GetProperty(field)?.GetValue(record) as string;
                        var doneBy = WorkflowRules.Modules[module].Steps.FirstOrDefault(s => s.Records == field);
                        if (string.IsNullOrWhiteSpace(who) || doneBy == null)
                            continue;
                        var u = await _users.FindByNameAsync(who) ?? await _users.FindByEmailAsync(who);
                        if (u == null || !(await _users.GetRolesAsync(u)).Any(step.Roles.Contains))
                            continue;   // they could not do this step anyway
                        var entry = (u != null ? TaskAssignmentService.DisplayName(u) : who, doneBy.Task);
                        if (!alreadyOut.Contains(entry))
                            alreadyOut.Add(entry);
                    }
                plan.TryGetValue(step.From, out var a);
                rows.Add(new Row
                {
                    Stage = step.From,
                    Task = step.Task,
                    StageValue = TaskAssignmentService.StageValue(record, step.From),
                    IsNow = step.From == state,
                    Assignment = a,
                    Eligible = !canAssign ? new()
                             : step.From == state ? (await _svc.EligibleAsync(module, record, step.From)).Select(P).ToList()
                             : (await _svc.EligibleForPlanAsync(module, record, route, step.From)).Select(P).ToList(),
                    Roles = step.Roles.Where(r => r != OpenQMS.Authorization.Constants.AdministratorsRole).ToList(),
                    NotSameAs = TaskAssignmentService.MustDifferFrom(route, step).Select(x => (x.From, x.Task)).ToList(),
                    RaiserOnlyIfOtherAt = review != null ? (review.From, review.Task) : null,
                    AuthorToo = step.AlsoUserIn != null,
                    AlreadyOut = alreadyOut,
                });
            }

            var msg = TempData[AssignmentFilter.MessageKey] as string;
            var parts = msg?.Split('|', 2);
            var model = new Model
            {
                Module = module,
                RecordId = recordId,
                Rows = rows,
                CanAssign = canAssign,
                FourEyesOn = await _svc.FourEyesOnPublicAsync(),
                RaiserId = FourEyesRaiser(module, record) is { } raiser ? (await _users.FindByNameAsync(raiser) ?? await _users.FindByEmailAsync(raiser))?.Id : null,
                OpenDialog = canAssign && HttpContext.Request.Query.ContainsKey("plan"),
                Next = next,
                Planned = planned,
                MessageType = parts?.Length == 2 ? parts[0] : null,
                Message = parts?.Length == 2 ? parts[1] : null,
            };
            return View(model);
        }

        private static string? FourEyesRaiser(string module, object record) =>
            record.GetType().GetProperty(WorkflowRules.Modules[module].RaisedBy)?.GetValue(record) as string;
    }
}
