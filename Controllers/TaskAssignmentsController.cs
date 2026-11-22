using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenQMS.Localization;
using OpenQMS.Models;
using OpenQMS.Services.Workflow;

namespace OpenQMS.Controllers
{
    /// <summary>Assign, reassign, unassign and plan the people responsible for a record's steps (no e-signature:
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
                ? "success|" + "The responsibilities have been saved."
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
                ? "success|" + "The responsibilities have been saved."
                : "warning|" + "Nothing was changed.";
            return Redirect($"/{module}/Details/{recordId}");
        }

        /// <summary>Saves the responsibilities form: who does the step the record is at now and the steps still ahead.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Plan(string module, int recordId, List<TaskAssignmentService.PlanEntry> steps)
        {
            if (!TaskAssignmentService.IsWorkflowModule(module)) return NotFound();
            var record = await _svc.LoadRecordAsync(module, recordId);
            if (record == null) return NotFound();
            if (!await _svc.CanAssignAsync(module, record, User)) return Forbid();

            var (error, warnings, changed) = await _svc.SavePlanAsync(module, record, steps ?? new(), _users.GetUserName(User)!);
            TempData[AssignmentFilter.MessageKey] =
                error != null ? "danger|" + error
                : warnings.Count > 0 ? "warning|" + warnings[0]
                : changed ? "success|" + "The responsibilities have been saved."
                : "info|" + "Nothing was changed.";
            return Redirect($"/{module}/Details/{recordId}");
        }
    }
}
