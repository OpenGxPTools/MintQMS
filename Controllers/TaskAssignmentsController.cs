using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenQMS.Localization;
using OpenQMS.Models;
using OpenQMS.Services.Workflow;

namespace OpenQMS.Controllers
{
    /// <summary>Assign, reassign and unassign the person responsible for a record's next step (no e-signature:
    /// assignment is information for accountability, not a GMP signature; each change is in the record's audit trail).</summary>
    [Authorize]
    public class TaskAssignmentsController : Controller
    {
        private readonly TaskAssignmentService _svc;
        private readonly UserManager<AppUser> _users;

        public TaskAssignmentsController(TaskAssignmentService svc, UserManager<AppUser> users)
        {
            _svc = svc;
            _users = users;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(string module, int recordId, int userId, DateTime? dueDate, string? note)
        {
            if (!TaskAssignmentService.IsWorkflowModule(module)) return NotFound();
            var record = await _svc.LoadRecordAsync(module, recordId);
            if (record == null) return NotFound();
            if (!await _svc.CanAssignAsync(module, record, User)) return Forbid();

            var why = await _svc.AssignAsync(module, record, userId, dueDate, note, _users.GetUserName(User)!);
            TempData[AssignmentFilter.MessageKey] = why == null
                ? "success|" + "The person responsible for the next step has been saved."
                : "danger|" + why;
            return Redirect($"/{module}/Details/{recordId}");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Unassign(string module, int recordId)
        {
            if (!TaskAssignmentService.IsWorkflowModule(module)) return NotFound();
            var record = await _svc.LoadRecordAsync(module, recordId);
            if (record == null) return NotFound();
            if (!await _svc.CanAssignAsync(module, record, User)) return Forbid();

            var done = await _svc.UnassignAsync(module, record, _users.GetUserName(User)!);
            TempData[AssignmentFilter.MessageKey] = done
                ? "success|" + "No one is named for the next step now."
                : "warning|" + "No one was named for the next step.";
            return Redirect($"/{module}/Details/{recordId}");
        }
    }
}
