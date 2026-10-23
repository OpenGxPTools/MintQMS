using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class PVSignalReviewController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PVSignalReviewController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Builds the "Assigned To" dropdown — only users with regulatory module access (mirrors LC/RI pattern).
        private async Task PopulateAssigneeList(string? selectedId)
        {
            var regulatoryRoles = new[] { "Administrator", "RegulatoryManager", "RegulatoryUser" };
            var regulatoryUsers = new List<AppUser>();
            foreach (var role in regulatoryRoles)
                regulatoryUsers.AddRange(await _userManager.GetUsersInRoleAsync(role));

            var users = regulatoryUsers
                .DistinctBy(u => u.Id)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .ToList();

            ViewBag.UsersList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                users.Select(u => new { Id = u.Id.ToString(), Name = $"{u.FirstName} {u.LastName} ({u.UserName})".Trim() }),
                "Id", "Name",
                selectedId);
        }

        // Resolves a stored AssignedTo user-id to a display name (legacy free-text values fall back as-is).
        private async Task<string?> ResolveAssigneeName(string? assignedTo)
        {
            if (string.IsNullOrEmpty(assignedTo)) return null;
            // The AppUser key is an int — only look up numeric ids so a legacy free-text
            // value doesn't throw a FormatException inside FindByIdAsync.
            var user = int.TryParse(assignedTo, out _)
                ? await _userManager.FindByIdAsync(assignedTo)
                : null;
            return user != null ? $"{user.FirstName} {user.LastName}".Trim() : assignedTo;
        }

        // GET: PVSignalReview
        public async Task<IActionResult> Index()
        {
            var signals = await _context.PVSignalReview
                .OrderByDescending(s => s.DetectedOn)
                .ToListAsync();

            // Resolve assignee user-ids to display names for the list
            var nameMap = await _userManager.Users
                .ToDictionaryAsync(u => u.Id.ToString(), u => $"{u.FirstName} {u.LastName}".Trim());
            ViewBag.AssigneeNames = nameMap;

            return View(signals);
        }

        // GET: PVSignalReview/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var signal = await _context.PVSignalReview
                .Include(s => s.AuditLogs)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (signal == null) return NotFound();

            // Load linked AEs sharing the same MedDRA PT
            ViewBag.LinkedAEs = await _context.AdverseEvent
                .Where(a => a.MedDRAPT == signal.MedDRAPT &&
                            a.Status != AdverseEvent.AEStatus.Voided)
                .OrderByDescending(a => a.ReportDate)
                .ToListAsync();

            // Load linked CAPA if any
            if (signal.LinkedCapaId.HasValue)
                ViewBag.LinkedCapa = await _context.Capa.FindAsync(signal.LinkedCapaId.Value);

            ViewBag.AssignedToName = await ResolveAssigneeName(signal.AssignedTo);

            return View(signal);
        }

        // GET: PVSignalReview/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var signal = await _context.PVSignalReview.FindAsync(id);
            if (signal == null) return NotFound();
            await PopulateAssigneeList(signal.AssignedTo);
            return View(signal);
        }

        // POST: PVSignalReview/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int id, PVSignalReview signal)
        {
            if (id != signal.Id) return NotFound();

            // Preserve system-set fields
            ModelState.Remove("DetectedBy");
            ModelState.Remove("TriggerCount");
            ModelState.Remove("CreatedOn");

            if (!ModelState.IsValid)
            {
                await PopulateAssigneeList(signal.AssignedTo);
                return View(signal);
            }

            var existing = await _context.PVSignalReview.FindAsync(id);
            if (existing == null) return NotFound();

            // Field-level change tracking for the audit log (capture before mutating the tracked entity)
            var diff = new List<string>();
            OpenQMS.Services.AuditDiffHelper.Track(diff,     "Assigned To",      existing.AssignedTo,      signal.AssignedTo);
            OpenQMS.Services.AuditDiffHelper.Track(diff,     "Assessment Notes", existing.AssessmentNotes, signal.AssessmentNotes);
            OpenQMS.Services.AuditDiffHelper.TrackEnum(diff, "Conclusion",       existing.Conclusion,      signal.Conclusion);

            // Status is driven by the workflow actions on the Details page, not the Edit form;
            // Edit only captures the assessment (assignee, notes, conclusion).
            existing.AssignedTo = signal.AssignedTo;
            existing.AssessmentNotes = signal.AssessmentNotes;
            existing.Conclusion = signal.Conclusion;

            await _context.SaveChangesAsync();

            if (diff.Count > 0) AddAuditLog(id, "Edited", existing.Status.ToString(), existing.Status.ToString(), OpenQMS.Services.AuditDiffHelper.Build(diff));
            await _context.SaveChangesAsync();

            TempData["Success"] = "Signal review updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Workflow transition (one-click; stamps reviewer on conclusion) ───
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Transition(int id, PVSignalReview.SignalStatus newStatus)
        {
            var signal = await _context.PVSignalReview.FindAsync(id);
            if (signal == null) return NotFound();

            if (!IsValidSignalTransition(signal.Status, newStatus))
            {
                TempData["Error"] = "Invalid status transition.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var oldStatus = signal.Status.ToString();
            signal.Status = newStatus;

            // Keep the conclusion aligned with a confirm/refute decision.
            if (newStatus == PVSignalReview.SignalStatus.Confirmed)
                signal.Conclusion = PVSignalReview.SignalConclusion.Confirmed;
            else if (newStatus == PVSignalReview.SignalStatus.Refuted)
                signal.Conclusion = PVSignalReview.SignalConclusion.Refuted;

            // Stamp the reviewer once the signal reaches a concluded/closed state.
            if (newStatus is PVSignalReview.SignalStatus.Confirmed
                          or PVSignalReview.SignalStatus.Refuted
                          or PVSignalReview.SignalStatus.Closed)
            {
                signal.ReviewedOn ??= DateTime.UtcNow;
                signal.ReviewedBy ??= _userManager.GetUserName(User);
            }

            await _context.SaveChangesAsync();
            AddAuditLog(id, "Status Changed", oldStatus, newStatus.ToString());
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Signal status updated to {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private static bool IsValidSignalTransition(
            PVSignalReview.SignalStatus from, PVSignalReview.SignalStatus to) => (from, to) switch
        {
            (PVSignalReview.SignalStatus.Open,        PVSignalReview.SignalStatus.UnderReview) => true,
            (PVSignalReview.SignalStatus.UnderReview, PVSignalReview.SignalStatus.Open)        => true,
            (PVSignalReview.SignalStatus.UnderReview, PVSignalReview.SignalStatus.Confirmed)   => true,
            (PVSignalReview.SignalStatus.UnderReview, PVSignalReview.SignalStatus.Refuted)     => true,
            (PVSignalReview.SignalStatus.Confirmed,   PVSignalReview.SignalStatus.Closed)      => true,
            (PVSignalReview.SignalStatus.Refuted,     PVSignalReview.SignalStatus.Closed)      => true,
            _ => false
        };

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.PVSignalAuditLog.Add(new PVSignalAuditLog
            {
                ParentId = parentId,
                Action = action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                PerformedBy = User.Identity?.Name ?? "System",
                PerformedOn = DateTime.UtcNow,
                Notes = notes
            });
        }
    }
}
