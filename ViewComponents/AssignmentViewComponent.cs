using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenQMS.Models;
using OpenQMS.Services;
using OpenQMS.Services.Workflow;

namespace OpenQMS.ViewComponents
{
    /// <summary>
    /// The "Responsible for the next step" panel on a workflow record's page, and the list of people each of the
    /// page's signature dialogs can name for the step after it. Usage in a Details view:
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

        public sealed class Model
        {
            public string Module { get; init; } = "";
            public int RecordId { get; init; }
            public string Stage { get; init; } = "";
            public bool HasTasks { get; init; }
            public TaskAssignment? Current { get; init; }
            public bool CanAssign { get; init; }
            public List<Person> Eligible { get; init; } = new();
            /// <summary>Signature dialog form action ("/Deviations/AcceptDeviation") → who can be named for the step after it.</summary>
            public Dictionary<string, List<Person>> Next { get; init; } = new();
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

            var next = new Dictionary<string, List<Person>>();
            if (me != null && !User.IsInRole(OpenQMS.Authorization.Constants.ReadOnlyRole))
            {
                foreach (var step in WorkflowRules.Modules[module].Steps.Where(s => s.From == state))
                {
                    var nextTasks = WorkflowRules.TasksFrom(module, step.To, record);
                    if (nextTasks.Count == 0 || next.ContainsKey($"/{module}/{step.Action}"))
                        continue;
                    // whoever signs this step has just done the work the next step may review
                    var exclude = nextTasks.Any(t => t.FourEyes != null) ? me : null;
                    next[$"/{module}/{step.Action}"] = (await _svc.EligibleAsync(module, record, step.To, exclude)).Select(P).ToList();
                }
            }

            var msg = TempData[AssignmentFilter.MessageKey] as string;
            var parts = msg?.Split('|', 2);
            var model = new Model
            {
                Module = module,
                RecordId = recordId,
                Stage = EnumHelper.Display((Enum)record.GetType().GetProperty("Status")!.GetValue(record)!),
                HasTasks = WorkflowRules.TasksFrom(module, state, record).Count > 0,
                Current = await _svc.CurrentAsync(module, recordId, state),
                CanAssign = await _svc.CanAssignAsync(module, record, HttpContext.User),
                Eligible = (await _svc.EligibleAsync(module, record)).Select(P).ToList(),
                Next = next,
                MessageType = parts?.Length == 2 ? parts[0] : null,
                Message = parts?.Length == 2 ? parts[1] : null,
            };
            return View(model);
        }
    }
}
