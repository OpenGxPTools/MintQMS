/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2025  C-realize IT Services SRL (https://www.c-realize.com)
This program is offered under a commercial and under the AGPL license.
*/

using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Localization;
using OpenQMS.Models;
using OpenQMS.Services;
using OpenQMS.Services.Workflow;

using static OpenQMS.Localization.RecordText;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,Auditor,Trainer,ReadOnly,OperationsUser")]
    public class MaintenancesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public MaintenancesController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            IPasswordHasher<AppUser> passwordHasher)
        {
            _context = context;
            _userManager = userManager;
            _passwordHasher = passwordHasher;
        }

        private void PopulateViewData()
        {
            ViewData["Assets"] = _context.Asset.Where(x => !x.IsDeleted && x.Status == Asset.AssetStatus.Approved).OrderBy(x => x.Name).ToList();
            ViewData["Processes"] = _context.Process.Where(x => !x.IsDeleted && x.Status == Process.ProcessStatus.Approved).OrderBy(x => x.Name).ToList();
            ViewData["Deviations"] = _context.Deviation
                .Where(x => !x.IsDeleted && (
                    x.Status == Deviation.DeviationStatus.Accepted ||
                    x.Status == Deviation.DeviationStatus.Resolution ||
                    x.Status == Deviation.DeviationStatus.Approved))
                .OrderByDescending(x => x.IdentifiedOn)
                .Take(50)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.DeviationId + " — " + x.Title })
                .ToList();
            ViewData["Capas"] = _context.Capa!
                .Where(x => !x.IsDeleted && (
                    x.Status == Capa.CapaStatus.Assessment ||
                    x.Status == Capa.CapaStatus.Accepted ||
                    x.Status == Capa.CapaStatus.PendingApproval ||
                    x.Status == Capa.CapaStatus.Implementation ||
                    x.Status == Capa.CapaStatus.Approved))
                .OrderByDescending(x => x.DeterminedOn)
                .Take(50)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.CapaId + " — " + x.Title })
                .ToList();
        }

        private async Task SyncMaintenanceJunctions(int maintenanceId, int[] deviationIds, int[] capaIds)
        {
            _context.MaintenanceDeviation.RemoveRange(await _context.MaintenanceDeviation.Where(x => x.MaintenanceId == maintenanceId).ToListAsync());
            _context.MaintenanceCapa.RemoveRange(await _context.MaintenanceCapa.Where(x => x.MaintenanceId == maintenanceId).ToListAsync());
            foreach (var id in deviationIds.Distinct())
                _context.MaintenanceDeviation.Add(new MaintenanceDeviation { MaintenanceId = maintenanceId, DeviationId = id });
            foreach (var id in capaIds.Distinct())
                _context.MaintenanceCapa.Add(new MaintenanceCapa { MaintenanceId = maintenanceId, CapaId = id });
        }

        private static string JoinLinked<T>(ICollection<T>? junctions, Func<T, string?> getName, string? legacyFallback)
        {
            if (junctions?.Any() == true)
                return string.Join("; ", junctions.Select(getName).Where(n => !string.IsNullOrEmpty(n)));
            return legacyFallback ?? "";
        }

        private string GenerateMaintenanceId()
        {
            var allIds = _context.Maintenance
                .AsEnumerable()
                .Select(x =>
                {
                    if (!string.IsNullOrEmpty(x.MaintenanceId))
                    {
                        var parts = x.MaintenanceId.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var n)) return n;
                    }
                    return 0;
                });
            var next = (allIds.Any() ? allIds.Max() : 0) + 1;
            return $"MNT-{next.ToString().PadLeft(4, '0')}";
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MaintenanceAuditLog.Add(new MaintenanceAuditLog
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

        private async Task<bool> VerifyCredentials(string email, string pwd)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrEmpty(pwd)) return false;
            return _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) == PasswordVerificationResult.Success;
        }

        private DateTime CalculateNextDate(DateTime currentDate, Maintenance.MaintenanceFrequency frequency) =>
            frequency switch
            {
                Maintenance.MaintenanceFrequency.Weekly     => currentDate.AddDays(7),
                Maintenance.MaintenanceFrequency.Monthly    => currentDate.AddMonths(1),
                Maintenance.MaintenanceFrequency.Quarterly  => currentDate.AddMonths(3),
                Maintenance.MaintenanceFrequency.SemiAnnual => currentDate.AddMonths(6),
                Maintenance.MaintenanceFrequency.Annual     => currentDate.AddYears(1),
                _                                           => currentDate
            };

        private static bool IsFrequencyValid(Maintenance.MaintenanceType type, Maintenance.MaintenanceFrequency freq) =>
            type switch
            {
                // Preventive is planned — no "As Needed"
                Maintenance.MaintenanceType.Preventive    => freq != Maintenance.MaintenanceFrequency.AsNeeded,
                // Corrective is reactive — no recurring schedules
                Maintenance.MaintenanceType.Corrective    => freq is Maintenance.MaintenanceFrequency.OneTime
                                                                   or Maintenance.MaintenanceFrequency.AsNeeded,
                Maintenance.MaintenanceType.Calibration   => freq is Maintenance.MaintenanceFrequency.OneTime
                                                                   or Maintenance.MaintenanceFrequency.Weekly
                                                                   or Maintenance.MaintenanceFrequency.Monthly
                                                                   or Maintenance.MaintenanceFrequency.Quarterly
                                                                   or Maintenance.MaintenanceFrequency.SemiAnnual
                                                                   or Maintenance.MaintenanceFrequency.Annual,
                // Qualification is event-triggered, not on a recurring schedule
                Maintenance.MaintenanceType.Qualification => freq is Maintenance.MaintenanceFrequency.OneTime
                                                                   or Maintenance.MaintenanceFrequency.AsNeeded,
                // Cleaning and Inspection cover all except As Needed for Cleaning
                Maintenance.MaintenanceType.Cleaning      => freq != Maintenance.MaintenanceFrequency.AsNeeded,
                Maintenance.MaintenanceType.Inspection    => true,
                _                                         => true
            };

        // GET: Maintenances/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var maintenances = await _context.Maintenance
                .Where(m => !m.IsDeleted)
                .Include(m => m.Asset)
                .Include(m => m.AuditLogs)
                .ToListAsync();

            var today = SiteTime.Today;

            ViewBag.Total = maintenances.Count;
            ViewBag.StatusCounts = Enum.GetValues<Maintenance.MaintenanceStatus>()
                .ToDictionary(s => s.ToString(), s => maintenances.Count(m => m.Status == s));
            ViewBag.OpenCount = maintenances.Count(m =>
                m.Status != Maintenance.MaintenanceStatus.Reviewed &&
                m.Status != Maintenance.MaintenanceStatus.Approved &&
                m.Status != Maintenance.MaintenanceStatus.Overdue &&
                m.Status != Maintenance.MaintenanceStatus.Suspended);
            ViewBag.SuspendedCount = maintenances.Count(m => m.Status == Maintenance.MaintenanceStatus.Suspended);
            ViewBag.ReviewedCount = maintenances.Count(m =>
                m.Status == Maintenance.MaintenanceStatus.Reviewed ||
                m.Status == Maintenance.MaintenanceStatus.Approved);

            // Overdue: scheduled date past but not yet started/approved
            ViewBag.OverdueCount = maintenances.Count(m =>
                (m.Status == Maintenance.MaintenanceStatus.Scheduled || m.Status == Maintenance.MaintenanceStatus.Overdue)
                && m.ScheduledDate.Date < today);

            ViewBag.TypeCounts = Enum.GetValues<Maintenance.MaintenanceType>()
                .ToDictionary(t => t.ToString(), t => maintenances.Count(m => m.Type == t));

            // Overdue list
            ViewBag.Overdue = maintenances
                .Where(m => (m.Status == Maintenance.MaintenanceStatus.Scheduled || m.Status == Maintenance.MaintenanceStatus.Overdue)
                    && m.ScheduledDate.Date < today)
                .OrderBy(m => m.ScheduledDate)
                .Take(10).ToList();

            // Upcoming: scheduled within the maintenance due warning window (System Settings)
            var maintWarnDays = (await _context.SystemSettings.FindAsync(1))?.MaintenanceDueWarningDays ?? 30;
            ViewBag.MaintenanceDueWarningDays = maintWarnDays;
            ViewBag.Upcoming = maintenances
                .Where(m => m.Status == Maintenance.MaintenanceStatus.Scheduled
                    && m.ScheduledDate.Date >= today
                    && m.ScheduledDate.Date <= today.AddDays(maintWarnDays))
                .OrderBy(m => m.ScheduledDate)
                .Take(10).ToList();

            // Pending review
            ViewBag.PendingReview = maintenances
                .Where(m => m.Status == Maintenance.MaintenanceStatus.Completed)
                .OrderBy(m => m.PerformedOn)
                .Take(10).ToList();

            ViewBag.RecentActivity = maintenances
                .Where(m => m.AuditLogs != null)
                .SelectMany(m => m.AuditLogs!.Select(l => new { Log = l, Maintenance = m }))
                .OrderByDescending(x => x.Log.PerformedOn)
                .Take(12)
                .Select(x => new { x.Log, MaintenanceId = x.Maintenance.MaintenanceId, RecordId = x.Maintenance.Id, Title = x.Maintenance.Title })
                .ToList();

            return View();
        }

        // GET: Maintenances
        public async Task<IActionResult> Index()
        {
            var maintenances = await _context.Maintenance
                .Where(m => !m.IsDeleted)
                .Include(m => m.Asset)
                .Include(m => m.Process)
                .OrderByDescending(m => m.ScheduledDate)
                .ToListAsync();
            return View(maintenances);
        }

        // GET: Maintenances/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenance = await _context.Maintenance
                .Include(m => m.Asset)
                .Include(m => m.Process)
                .Include(m => m.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(m => m.LinkedCapas).ThenInclude(x => x.Capa)
                .Include(m => m.Deviation)
                .Include(m => m.Capa)
                .Include(m => m.Attachments)
                .Include(m => m.AuditLogs)
                .Include(m => m.ParentMaintenance)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (maintenance == null || maintenance.IsDeleted)
                return NotFound();

            // Find next occurrence in the recurring chain
            ViewBag.ChildMaintenance = await _context.Maintenance
                .Where(m => m.ParentMaintenanceId == id && !m.IsDeleted)
                .Select(m => new { m.Id, m.MaintenanceId, m.Status, m.ScheduledDate })
                .FirstOrDefaultAsync();

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.MaintenanceAttachment.DeletedOnly().Where(f => f.MaintenanceId == id).ToListAsync();
            return View(maintenance);
        }

        // GET: Maintenances/Create
        public IActionResult Create(int? assetId, int? deviationId, int? capaId)
        {
            PopulateViewData();
            var maintenance = new Maintenance
            {
                ScheduledDate = DateTime.UtcNow.AddDays(1),
                Status = Maintenance.MaintenanceStatus.Scheduled
            };
            if (assetId.HasValue) maintenance.AssetId = assetId.Value;
            ViewBag.SelectedDeviationIds = deviationId.HasValue ? new List<int> { deviationId.Value } : new List<int>();
            ViewBag.SelectedCapaIds = capaId.HasValue ? new List<int> { capaId.Value } : new List<int>();
            return View(maintenance);
        }

        // POST: Maintenances/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Title,Description,AssetId,ProcessId,Type,Frequency,ScheduledDate,WorkPerformed,PartsUsed,Cost")] Maintenance maintenance,
            int[]? selectedDeviations, int[]? selectedCapas)
        {
            if (maintenance.ScheduledDate.Date < SiteTime.Today)
                ModelState.AddModelError(nameof(maintenance.ScheduledDate), this.T("Scheduled date must be today or in the future."));

            if (!IsFrequencyValid(maintenance.Type, maintenance.Frequency))
                ModelState.AddModelError(nameof(maintenance.Frequency), this.T("'{0}' is not a valid frequency for {1} maintenance.", this.T(EnumHelper.Display(maintenance.Frequency)), this.T(EnumHelper.Display(maintenance.Type))));

            if (ModelState.IsValid)
            {
                maintenance.MaintenanceId = GenerateMaintenanceId();
                maintenance.ScheduledBy = _userManager.GetUserName(User) ?? "System";
                maintenance.ScheduledOn = DateTime.UtcNow;
                maintenance.Status = Maintenance.MaintenanceStatus.Scheduled;
                maintenance.DeviationId = null; maintenance.CapaId = null;

                if (maintenance.Frequency != Maintenance.MaintenanceFrequency.OneTime
                    && maintenance.Frequency != Maintenance.MaintenanceFrequency.AsNeeded)
                    maintenance.NextScheduledDate = CalculateNextDate(maintenance.ScheduledDate, maintenance.Frequency);

                _context.Add(maintenance);
                await _context.SaveChangesAsync();

                await SyncMaintenanceJunctions(maintenance.Id,
                    selectedDeviations ?? Array.Empty<int>(),
                    selectedCapas ?? Array.Empty<int>());

                AddAuditLog(maintenance.Id, "Created", null, maintenance.Status.ToString(),
                    $"ID: {maintenance.MaintenanceId}; Title: {maintenance.Title}; Type: {maintenance.Type}; Scheduled by: {maintenance.ScheduledBy}");
                await _context.SaveChangesAsync();

                TempData["Success"] = this.T("Maintenance {0} created.", maintenance.MaintenanceId);
                return RedirectToAction(nameof(Details), new { id = maintenance.Id });
            }

            PopulateViewData();
            ViewBag.SelectedDeviationIds = selectedDeviations?.ToList() ?? new List<int>();
            ViewBag.SelectedCapaIds      = selectedCapas?.ToList()      ?? new List<int>();
            return View(maintenance);
        }

        // GET: Maintenances/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (id == null)
                return NotFound();

            var maintenance = await _context.Maintenance
                .Include(m => m.Asset)
                .Include(m => m.Process)
                .Include(m => m.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(m => m.LinkedCapas).ThenInclude(x => x.Capa)
                .Include(m => m.Deviation)
                .Include(m => m.Capa)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (maintenance == null)
                return NotFound();

            if (maintenance.Status == Maintenance.MaintenanceStatus.Reviewed ||
                maintenance.Status == Maintenance.MaintenanceStatus.Approved ||
                maintenance.Status == Maintenance.MaintenanceStatus.Overdue)
                return RedirectToAction(nameof(Details), new { id });

            ViewBag.SelectedDeviationIds = maintenance.LinkedDeviations?.Any() == true
                ? maintenance.LinkedDeviations.Select(x => x.DeviationId).ToList()
                : (maintenance.DeviationId.HasValue ? new List<int> { maintenance.DeviationId.Value } : new List<int>());
            ViewBag.SelectedCapaIds = maintenance.LinkedCapas?.Any() == true
                ? maintenance.LinkedCapas.Select(x => x.CapaId).ToList()
                : (maintenance.CapaId.HasValue ? new List<int> { maintenance.CapaId.Value } : new List<int>());

            PopulateViewData();
            return View(maintenance);
        }

        // POST: Maintenances/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int[]? selectedDeviations, int[]? selectedCapas)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();
            var snap        = await _context.Maintenance.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null || snap == null) return NotFound();

            if (maintenance.Status == Maintenance.MaintenanceStatus.Reviewed ||
                maintenance.Status == Maintenance.MaintenanceStatus.Approved ||
                maintenance.Status == Maintenance.MaintenanceStatus.Overdue)
                return RedirectToAction(nameof(Details), new { id });

            var previousReviewComments = maintenance.ReviewComments;
            var updated = await TryUpdateModelAsync(maintenance, "",
                m => m.Title, m => m.Description,
                m => m.AssetId, m => m.ProcessId,
                m => m.Type, m => m.Frequency, m => m.ScheduledDate,
                m => m.WorkPerformed, m => m.PartsUsed, m => m.Cost,
                m => m.ReviewComments);

            // Only Admin/QualityManager/AuthorisedPerson may update review comments
            if (!User.IsInRole(Constants.AdministratorsRole) && !User.IsInRole(Constants.QualityManagerRole) && !User.IsInRole(Constants.AuthorisedPersonRole))
                maintenance.ReviewComments = previousReviewComments;

            if (updated && maintenance.Status == Maintenance.MaintenanceStatus.Scheduled
                && maintenance.ScheduledDate.Date < SiteTime.Today)
                ModelState.AddModelError(nameof(maintenance.ScheduledDate), this.T("Scheduled date must be today or in the future."));

            if (updated && !IsFrequencyValid(maintenance.Type, maintenance.Frequency))
                ModelState.AddModelError(nameof(maintenance.Frequency), this.T("'{0}' is not a valid frequency for {1} maintenance.", this.T(EnumHelper.Display(maintenance.Frequency)), this.T(EnumHelper.Display(maintenance.Type))));

            if (updated && ModelState.IsValid)
            {
                try
                {
                    // Recalculate next occurrence date if schedule-related fields changed
                    if (maintenance.Status == Maintenance.MaintenanceStatus.Scheduled)
                    {
                        if (maintenance.Frequency != Maintenance.MaintenanceFrequency.OneTime
                            && maintenance.Frequency != Maintenance.MaintenanceFrequency.AsNeeded)
                            maintenance.NextScheduledDate = CalculateNextDate(maintenance.ScheduledDate, maintenance.Frequency);
                        else
                            maintenance.NextScheduledDate = null;
                    }

                    // Capture linked-record associations before sync (for the audit diff)
                    var oldMnDeviationIds = await _context.MaintenanceDeviation.Where(x => x.MaintenanceId == maintenance.Id).Select(x => x.DeviationId).ToListAsync();
                    var oldMnCapaIds      = await _context.MaintenanceCapa.Where(x => x.MaintenanceId == maintenance.Id).Select(x => x.CapaId).ToListAsync();

                    maintenance.DeviationId = null; maintenance.CapaId = null;
                    await SyncMaintenanceJunctions(maintenance.Id,
                        selectedDeviations ?? Array.Empty<int>(),
                        selectedCapas ?? Array.Empty<int>());

                    var diff = new List<string>();
                    AuditDiffHelper.Track(diff,     "Title",          snap.Title,          maintenance.Title);
                    AuditDiffHelper.Track(diff,     "Description",    snap.Description,    maintenance.Description);
                    AuditDiffHelper.TrackEnum(diff, "Type",           snap.Type,            maintenance.Type);
                    AuditDiffHelper.TrackEnum(diff, "Frequency",      snap.Frequency,       maintenance.Frequency);
                    AuditDiffHelper.TrackDate(diff, "Scheduled Date", snap.ScheduledDate,   maintenance.ScheduledDate, "yyyy-MM-dd");
                    AuditDiffHelper.Track(diff,     "Work Performed", snap.WorkPerformed,  maintenance.WorkPerformed);
                    AuditDiffHelper.Track(diff,     "Parts Used",     snap.PartsUsed,      maintenance.PartsUsed);
                    AuditDiffHelper.Track<decimal>(diff, "Cost",      snap.Cost,            maintenance.Cost);
                    AuditDiffHelper.Track(diff,     "Review Comments",snap.ReviewComments, maintenance.ReviewComments);
                    AuditDiffHelper.TrackSet(diff, "Linked Deviations", oldMnDeviationIds, selectedDeviations ?? Array.Empty<int>(),
                        ids => _context.Deviation.Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id, d => d.DeviationId));
                    AuditDiffHelper.TrackSet(diff, "Linked CAPAs",      oldMnCapaIds,      selectedCapas      ?? Array.Empty<int>(),
                        ids => _context.Capa.Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id, c => c.CapaId));
                    if (diff.Count > 0) AddAuditLog(maintenance.Id, "Edited", null, null, AuditDiffHelper.Build(diff));
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MaintenanceExists(maintenance.Id)) return NotFound();
                    else throw;
                }

                TempData["Success"] = this.T("Maintenance updated.");
                return RedirectToAction(nameof(Details), new { id });
            }

            PopulateViewData();
            ViewBag.SelectedDeviationIds = selectedDeviations?.ToList() ?? new List<int>();
            ViewBag.SelectedCapaIds      = selectedCapas?.ToList()      ?? new List<int>();
            return View(maintenance);
        }

        // GET: Maintenances/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenance = await _context.Maintenance
                .Include(m => m.Asset)
                .Include(m => m.Process)
                .Include(m => m.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(m => m.LinkedCapas).ThenInclude(x => x.Capa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (maintenance == null)
                return NotFound();

            if (maintenance.Status == Maintenance.MaintenanceStatus.InProgress ||
                maintenance.Status == Maintenance.MaintenanceStatus.Completed ||
                maintenance.Status == Maintenance.MaintenanceStatus.Reviewed  ||
                maintenance.Status == Maintenance.MaintenanceStatus.Approved)
                return Forbid();

            return View(maintenance);
        }

        // POST: Maintenances/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var maintenance = await _context.Maintenance
                .Include(m => m.Attachments)
                .Include(m => m.AuditLogs)
                .Include(m => m.LinkedDeviations)
                .Include(m => m.LinkedCapas)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (maintenance != null)
            {
                if (maintenance.Status == Maintenance.MaintenanceStatus.InProgress ||
                    maintenance.Status == Maintenance.MaintenanceStatus.Completed  ||
                    maintenance.Status == Maintenance.MaintenanceStatus.Reviewed   ||
                    maintenance.Status == Maintenance.MaintenanceStatus.Approved)
                    return Forbid();

                // Nullify ParentMaintenanceId on any children to avoid dangling FK
                var children = await _context.Maintenance
                    .Where(m => m.ParentMaintenanceId == maintenance.Id)
                    .ToListAsync();
                foreach (var child in children)
                    child.ParentMaintenanceId = null;

                // Remove junction rows so no orphans remain after soft-delete
                if (maintenance.LinkedDeviations?.Any() == true)
                    _context.MaintenanceDeviation.RemoveRange(maintenance.LinkedDeviations);
                if (maintenance.LinkedCapas?.Any() == true)
                    _context.MaintenanceCapa.RemoveRange(maintenance.LinkedCapas);

                // Attachments (rows and files) are retained with the soft-deleted record.
                AddAuditLog(maintenance.Id, "Deleted", maintenance.Status.ToString(), null, maintenance.Title);
                maintenance.IsDeleted = true;
                maintenance.DeletedBy = _userManager.GetUserName(User);
                maintenance.DeletedOn = DateTime.UtcNow;
                _context.Maintenance.Update(maintenance);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Workflow transitions

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> StartWork(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();

            if (maintenance.Status != Maintenance.MaintenanceStatus.Scheduled
                && maintenance.Status != Maintenance.MaintenanceStatus.Overdue)
            {
                TempData["WorkflowError"] = this.T("Maintenance must be in Scheduled or Overdue status to start work.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = maintenance.Status.ToString();
            maintenance.Status = Maintenance.MaintenanceStatus.InProgress;
            maintenance.PerformedBy = _userManager.GetUserName(User) ?? email;
            maintenance.ActualStartTime = DateTime.UtcNow;

            AddAuditLog(maintenance.Id, "Work Started", from, maintenance.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Maintenance work started.");
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Maintenances/RecordWork/5
        // The person carrying out the work records what was done while the job is In Progress
        // (work performed, parts used, cost). Other fields stay with Edit (QM/AP/Admin).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> RecordWork(int id, string? workPerformed, string? partsUsed, decimal? cost)
        {
            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();
            if (maintenance.Status != Maintenance.MaintenanceStatus.InProgress)
            {
                TempData["WorkflowError"] = this.T("Work details can only be recorded while the maintenance is In Progress.");
                return RedirectToAction(nameof(Details), new { id });
            }
            if (cost is < 0)
            {
                TempData["WorkflowError"] = this.T("Cost cannot be negative.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var diff = new List<string>();
            var newWork  = string.IsNullOrWhiteSpace(workPerformed) ? null : workPerformed.Trim();
            var newParts = string.IsNullOrWhiteSpace(partsUsed) ? null : partsUsed.Trim();
            AuditDiffHelper.Track(diff,          "Work Performed", maintenance.WorkPerformed, newWork);
            AuditDiffHelper.Track(diff,          "Parts Used",     maintenance.PartsUsed,     newParts);
            AuditDiffHelper.Track<decimal>(diff, "Cost",           maintenance.Cost,          cost);
            if (diff.Count == 0)
            {
                TempData["Success"] = this.T("No changes to the work details.");
                return RedirectToAction(nameof(Details), new { id });
            }

            maintenance.WorkPerformed = newWork;
            maintenance.PartsUsed     = newParts;
            maintenance.Cost          = cost;
            AddAuditLog(maintenance.Id, "Work Recorded", null, null, AuditDiffHelper.Build(diff));
            await _context.SaveChangesAsync();
            TempData["Success"] = this.T("Work details recorded.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> CompleteWork(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();

            if (maintenance.Status != Maintenance.MaintenanceStatus.InProgress)
            {
                TempData["WorkflowError"] = this.T("Maintenance is not In Progress.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = maintenance.Status.ToString();
            maintenance.Status = Maintenance.MaintenanceStatus.Completed;
            maintenance.PerformedOn = DateTime.UtcNow;
            maintenance.ActualEndTime = DateTime.UtcNow;

            if (maintenance.ActualStartTime.HasValue)
                maintenance.DowntimeHours = (decimal)(maintenance.ActualEndTime.Value - maintenance.ActualStartTime.Value).TotalHours;

            AddAuditLog(maintenance.Id, "Work Completed", from, maintenance.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Maintenance work completed.");
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> ReviewWork(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();

            if (maintenance.Status != Maintenance.MaintenanceStatus.Completed)
            {
                TempData["WorkflowError"] = this.T("Maintenance is not in Completed status.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = maintenance.Status.ToString();
            if (await this.RefusalAsync("Maintenances", nameof(ReviewWork), maintenance) is { } fourEyes)
            {
                TempData["WorkflowError"] = fourEyes;
                return RedirectToAction(nameof(Details), new { id });
            }

            maintenance.Status = Maintenance.MaintenanceStatus.Reviewed;
            maintenance.ReviewedBy = _userManager.GetUserName(User) ?? email;
            maintenance.ReviewedOn = DateTime.UtcNow;

            var _maintSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            maintenance.RetainUntil = maintenance.ReviewedOn.Value.AddYears(_maintSets.MaintenanceRetentionYears);

            AddAuditLog(maintenance.Id, "Reviewed", from, maintenance.Status.ToString(), notes);

            // Auto-spawn next occurrence for recurring maintenance
            Maintenance? nextOccurrence = null;
            if (maintenance.Frequency != Maintenance.MaintenanceFrequency.OneTime &&
                maintenance.Frequency != Maintenance.MaintenanceFrequency.AsNeeded &&
                maintenance.NextScheduledDate.HasValue)
            {
                nextOccurrence = new Maintenance
                {
                    MaintenanceId       = GenerateMaintenanceId(),
                    ParentMaintenanceId = maintenance.Id,
                    AssetId             = maintenance.AssetId,
                    ProcessId           = maintenance.ProcessId,
                    Title               = maintenance.Title,
                    Description         = maintenance.Description,
                    Type                = maintenance.Type,
                    Frequency           = maintenance.Frequency,
                    Priority            = maintenance.Priority,
                    ScheduledDate       = maintenance.NextScheduledDate.Value,
                    ScheduledBy         = _userManager.GetUserName(User) ?? "System",
                    ScheduledOn         = DateTime.UtcNow,
                    Status              = Maintenance.MaintenanceStatus.Scheduled,
                };
                nextOccurrence.NextScheduledDate = CalculateNextDate(nextOccurrence.ScheduledDate, nextOccurrence.Frequency);
                _context.Add(nextOccurrence);
            }

            await _context.SaveChangesAsync();

            if (nextOccurrence != null)
            {
                AddAuditLog(nextOccurrence.Id, "Auto-scheduled", null, nextOccurrence.Status.ToString(),
                    $"Auto-generated from {maintenance.MaintenanceId}. Scheduled: {nextOccurrence.ScheduledDate:yyyy-MM-dd}");
                await _context.SaveChangesAsync();
                TempData["Success"] = this.T("Maintenance reviewed and closed. Next occurrence {0} scheduled for {1}.", nextOccurrence.MaintenanceId, nextOccurrence.ScheduledDate.ToString("dd MMM yyyy", DisplayDates.Culture));
            }
            else
            {
                TempData["Success"] = this.T("Maintenance reviewed and closed.");
            }

            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Send-back (rework) transitions

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> SendBackToScheduled(int id, string email, string pwd, string notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["WorkflowError"] = this.T("A reason is required to send back.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();
            if (maintenance.Status != Maintenance.MaintenanceStatus.InProgress)
            {
                TempData["WorkflowError"] = this.T("Maintenance is not In Progress.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = maintenance.Status.ToString();
            maintenance.Status = Maintenance.MaintenanceStatus.Scheduled;
            maintenance.PerformedBy = null;
            maintenance.ActualStartTime = null;

            AddAuditLog(maintenance.Id, "Sent Back to Scheduled", from, maintenance.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Maintenance sent back to Scheduled for rework.");
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> SendBackToInProgress(int id, string email, string pwd, string notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["WorkflowError"] = this.T("A reason is required to send back.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();
            if (maintenance.Status != Maintenance.MaintenanceStatus.Completed)
            {
                TempData["WorkflowError"] = this.T("Maintenance is not in Completed status.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = maintenance.Status.ToString();
            maintenance.Status = Maintenance.MaintenanceStatus.InProgress;
            maintenance.PerformedOn = null;
            maintenance.ActualEndTime = null;
            maintenance.DowntimeHours = null;

            AddAuditLog(maintenance.Id, "Sent Back to In Progress", from, maintenance.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Maintenance sent back to In Progress for rework.");
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> SendBackToCompleted(int id, string email, string pwd, string notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["WorkflowError"] = this.T("A reason is required to send back.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();
            if (maintenance.Status != Maintenance.MaintenanceStatus.Reviewed)
            {
                TempData["WorkflowError"] = this.T("Maintenance is not in Reviewed status.");
                return RedirectToAction(nameof(Details), new { id });
            }

            await ReturnToCompletedAsync(maintenance, "Sent Back to Completed", notes, "sent back to Completed for re-review");

            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Maintenance sent back to Completed for re-review.");
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // A reviewed record goes back to Completed for re-review: the review is cleared and the next occurrence that
        // was scheduled automatically at review is cancelled. Used by Send Back to Completed and Administrative Reopen.
        private async Task ReturnToCompletedAsync(Maintenance maintenance, string auditAction, string notes, string why)
        {
            var from = maintenance.Status.ToString();
            maintenance.Status = Maintenance.MaintenanceStatus.Completed;
            maintenance.ReviewedBy = null;
            maintenance.ReviewedOn = null;
            maintenance.ReviewComments = null;
            maintenance.RetainUntil = null;

            AddAuditLog(maintenance.Id, auditAction, from, maintenance.Status.ToString(), notes);

            // Cancel any auto-spawned next occurrence that was created when this record was reviewed
            var autoSpawnedChild = await _context.Maintenance
                .FirstOrDefaultAsync(m => m.ParentMaintenanceId == maintenance.Id
                    && !m.IsDeleted
                    && m.Status == Maintenance.MaintenanceStatus.Scheduled);
            if (autoSpawnedChild != null)
            {
                autoSpawnedChild.IsDeleted = true;
                autoSpawnedChild.DeletedBy = _userManager.GetUserName(User);
                autoSpawnedChild.DeletedOn = DateTime.UtcNow;
                AddAuditLog(autoSpawnedChild.Id, "Auto-cancelled", autoSpawnedChild.Status.ToString(), null,
                    $"Cancelled because {maintenance.MaintenanceId} was {why}.");
            }
        }

        // Suspend / Reactivate

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Suspend(int id, string email, string pwd, string reason)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = this.T("A reason is required to suspend a maintenance schedule.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();

            var suspendable = new[]
            {
                Maintenance.MaintenanceStatus.Scheduled,
                Maintenance.MaintenanceStatus.Overdue,
                Maintenance.MaintenanceStatus.InProgress,
                Maintenance.MaintenanceStatus.Completed,
            };
            if (!suspendable.Contains(maintenance.Status))
            {
                TempData["WorkflowError"] = this.T("Maintenance cannot be suspended from '{0}' status.", this.T(EnumHelper.Display(maintenance.Status)));
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = maintenance.Status.ToString();
            maintenance.Status = Maintenance.MaintenanceStatus.Suspended;
            maintenance.SuspendedBy = _userManager.GetUserName(User) ?? email;
            maintenance.SuspendedOn = DateTime.UtcNow;
            maintenance.SuspensionReason = reason;

            AddAuditLog(maintenance.Id, "Suspended", from, maintenance.Status.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Maintenance schedule suspended.");
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Reactivate(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = this.T("Invalid credentials. Transition was not applied.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();
            if (maintenance.Status != Maintenance.MaintenanceStatus.Suspended)
            {
                TempData["WorkflowError"] = this.T("Maintenance is not suspended.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = maintenance.Status.ToString();
            maintenance.Status = Maintenance.MaintenanceStatus.Scheduled;
            maintenance.SuspendedBy = null;
            maintenance.SuspendedOn = null;
            maintenance.SuspensionReason = null;

            AddAuditLog(maintenance.Id, "Reactivated", from, maintenance.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Maintenance schedule reactivated.");
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }


        // Attachments

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int maintenanceId, IFormFile file, string? description)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = this.T("Please select a file to upload.");
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = maintenanceId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = this.T("File type not allowed. Accepted: PDF, Word, Excel, ZIP, JPG, PNG.");
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = maintenanceId });
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = this.T("File size exceeds the 25 MB limit.");
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = maintenanceId });
            }

            var maintenance = await _context.Maintenance.FindAsync(maintenanceId);
            if (maintenance == null) return NotFound();

            if (maintenance.Status == Maintenance.MaintenanceStatus.Reviewed || maintenance.Status == Maintenance.MaintenanceStatus.Approved)
            {
                TempData["Error"] = this.T("Attachments cannot be added to a reviewed or approved maintenance record.");
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = maintenanceId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "maintenances", maintenanceId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var safeName = Path.GetFileNameWithoutExtension(file.FileName).Replace(" ", "_");
            var uniqueFileName = $"{timestamp}_{safeName}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            _context.MaintenanceAttachment.Add(new MaintenanceAttachment
            {
                MaintenanceId = maintenanceId,
                FileName = file.FileName,
                FilePath = $"/uploads/maintenances/{maintenanceId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                Description = description
            });

            AddAuditLog(maintenanceId, "Attachment uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("File '{0}' uploaded.", file.FileName);
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = maintenanceId });
        }
        [RecordLanguage]

        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();

            var attachment = await _context.MaintenanceAttachment.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = this.T("File not found on server.");
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.MaintenanceId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.MaintenanceId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.MaintenanceId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Maintenance",
                attachment.MaintenanceId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }
        [RecordLanguage]

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.MaintenanceAttachment.FindAsync(id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = this.T("File not found on server.");
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.MaintenanceId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.MaintenanceId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.MaintenanceId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Maintenance",
                attachment.MaintenanceId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int maintenanceId, string? deletionReason)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var attachment = await _context.MaintenanceAttachment.FindAsync(id);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                maintenanceId = attachment.MaintenanceId;

                var maintenance = await _context.Maintenance.FindAsync(maintenanceId);
                if (maintenance != null && (maintenance.Status == Maintenance.MaintenanceStatus.Reviewed || maintenance.Status == Maintenance.MaintenanceStatus.Approved))
                {
                    TempData["Error"] = this.T("Attachments cannot be removed from a reviewed or approved maintenance record.");
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = maintenanceId });
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = this.T("A reason is required to delete an attachment.");
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = maintenanceId });
                }

                // Soft delete: the row and file are retained for traceability.
                attachment.IsDeleted      = true;
                attachment.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                AddAuditLog(maintenanceId, "Attachment deleted", null, null, $"{attachment.FileName} — Reason: {attachment.DeletionReason}");
                await _context.SaveChangesAsync();
                TempData["Success"] = this.T("Attachment deleted.");
            }

            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = maintenanceId });
        }

        // Export
        [RecordLanguage]
        public async Task<IActionResult> ExportRegister()
        {
            var maintenances = await _context.Maintenance
                .Where(m => !m.IsDeleted)
                .Include(m => m.Asset)
                .Include(m => m.Process)
                .OrderByDescending(m => m.ScheduledDate)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(Sheet(R("Maintenance Register")));

            var headers = new[] { R("Maintenance ID"), R("Title"), R("Asset"), R("Type"), R("Frequency"), R("Status"),
                R("Scheduled Date"), R("Performed By"), R("Performed On"), R("Actual Start"), R("Actual End"),
                R("Downtime (h)"), R("Parts Used"), R("Cost"), R("Reviewed By"), R("Next Scheduled"), R("Process") };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            ws.SheetView.FreezeRows(1);

            for (int i = 0; i < maintenances.Count; i++)
            {
                var m = maintenances[i];
                var row = i + 2;
                var fill = i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#F2F2F2");
                ws.Cell(row, 1).Value = m.MaintenanceId;
                ws.Cell(row, 2).Value = m.Title;
                ws.Cell(row, 3).Value = m.Asset?.Name ?? "";
                ws.Cell(row, 4).Value = RE(m.Type);
                ws.Cell(row, 5).Value = RE(m.Frequency);
                ws.Cell(row, 6).Value = RE(m.Status);
                ws.Cell(row, 7).Value = m.ScheduledDate.ToShortDateString();
                ws.Cell(row, 8).Value = m.PerformedBy ?? "";
                ws.Cell(row, 9).Value = m.PerformedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 10).Value = m.ActualStartTime.ToSite()?.ToString("yyyy-MM-dd HH:mm") ?? "";
                ws.Cell(row, 11).Value = m.ActualEndTime.ToSite()?.ToString("yyyy-MM-dd HH:mm") ?? "";
                ws.Cell(row, 12).Value = m.DowntimeHours?.ToString("F2") ?? "";
                ws.Cell(row, 13).Value = m.PartsUsed ?? "";
                ws.Cell(row, 14).Value = m.Cost?.ToString("F2") ?? "";
                ws.Cell(row, 15).Value = m.ReviewedBy ?? "";
                ws.Cell(row, 16).Value = m.NextScheduledDate?.ToShortDateString() ?? "";
                ws.Cell(row, 17).Value = m.Process?.Name ?? "";
                for (int c = 1; c <= headers.Length; c++)
                    ws.Cell(row, c).Style.Fill.BackgroundColor = fill;
            }

            ws.Columns().AdjustToContents();

            OpenQMS.Services.ExcelExportHelper.AddExportInfoSheet(wb, _userManager.GetUserName(User) ?? "Unknown", R("Maintenance Register"), maintenances.Count);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"MaintenanceRegister_{SiteTime.Now:yyyyMMdd}.xlsx");
        }
        [RecordLanguage]

        public async Task<IActionResult> ExportMaintenanceDetail(int id)
        {
            if (_context.Maintenance == null) return NotFound();
            var maintenance = await _context.Maintenance
                .Where(x => x.Id == id)
                .Include(x => x.Asset).Include(x => x.Process)
                .Include(x => x.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(x => x.LinkedCapas).ThenInclude(x => x.Capa)
                .Include(x => x.Deviation).Include(x => x.Capa)
                .FirstOrDefaultAsync();
            if (maintenance == null) return NotFound();

            string? L(string s) => s.Length > 0 ? s : null;
            var fields = new List<(string, string?)>
            {
                (R("Maintenance ID"),  maintenance.MaintenanceId),
                (R("Title"),           maintenance.Title),
                (R("Status"),          RE(maintenance.Status)),
                (R("Type"),            RE(maintenance.Type)),
                (R("Frequency"),       RE(maintenance.Frequency)),
                (R("Asset"),           maintenance.Asset?.Name),
                (R("Process"),         maintenance.Process?.Name),
                (R("Linked Deviations"), L(JoinLinked(maintenance.LinkedDeviations, x => x.Deviation != null ? x.Deviation.DeviationId + " — " + x.Deviation.Title : null, maintenance.Deviation != null ? maintenance.Deviation.DeviationId + " — " + maintenance.Deviation.Title : null))),
                (R("Linked CAPAs"),    L(JoinLinked(maintenance.LinkedCapas,    x => x.Capa != null ? x.Capa.CapaId + " — " + x.Capa.Title : null, maintenance.Capa != null ? maintenance.Capa.CapaId + " — " + maintenance.Capa.Title : null))),
                (R("Description"),     maintenance.Description),
                (R("Scheduled Date"),  maintenance.ScheduledDate.ToShortDateString()),
                (R("Scheduled By"),    maintenance.ScheduledBy),
                (R("Performed By"),    maintenance.PerformedBy),
                (R("Performed On"),    maintenance.PerformedOn.ToSite()?.ToShortDateString()),
                (R("Actual Start"),    maintenance.ActualStartTime.ToSite()?.ToString("yyyy-MM-dd HH:mm")),
                (R("Actual End"),      maintenance.ActualEndTime.ToSite()?.ToString("yyyy-MM-dd HH:mm")),
                (R("Downtime (hrs)"),  maintenance.DowntimeHours?.ToString("F2")),
                (R("Work Performed"),  maintenance.WorkPerformed),
                (R("Parts Used"),      maintenance.PartsUsed),
                (R("Cost"),            maintenance.Cost?.ToString("C")),
                (R("Review Comments"), maintenance.ReviewComments),
                (R("Reviewed By"),     maintenance.ReviewedBy),
                (R("Reviewed On"),     maintenance.ReviewedOn.ToSite()?.ToShortDateString()),
            };

            AddAuditLog(maintenance.Id, "Exported (PDF)", maintenance.Status.ToString(), maintenance.Status.ToString());
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate(R("Maintenance Detail Report"), maintenance.MaintenanceId, RE(maintenance.Status), fields,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Maintenance-{maintenance.MaintenanceId}.pdf");
        }
        [RecordLanguage]

        public async Task<IActionResult> ExportDetailInCSV(int id)
        {
            if (_context.Maintenance == null) return NotFound();
            var maintenance = await _context.Maintenance
                .Where(x => x.Id == id)
                .Include(x => x.Asset).Include(x => x.Process)
                .Include(x => x.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(x => x.LinkedCapas).ThenInclude(x => x.Capa)
                .Include(x => x.Deviation).Include(x => x.Capa)
                .FirstOrDefaultAsync();
            if (maintenance == null) return NotFound();

            AddAuditLog(maintenance.Id, "Exported (CSV)", maintenance.Status.ToString(), maintenance.Status.ToString());
            await _context.SaveChangesAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine(CsvHeader("Maintenance ID", "Title", "Status", "Type", "Frequency", "Asset", "Process", "Linked Deviations", "Linked CAPAs", "Description", "Scheduled Date", "Scheduled By", "Performed By", "Performed On", "Work Performed", "Parts Used", "Cost", "Review Comments", "Reviewed By", "Reviewed On"));
            builder.AppendLine($"{CsvSafe(maintenance.MaintenanceId)},{CsvSafe(maintenance.Title)},{RE(maintenance.Status)},{RE(maintenance.Type)},{RE(maintenance.Frequency)},{CsvSafe(maintenance.Asset?.Name)},{CsvSafe(maintenance.Process?.Name)},{CsvSafe(JoinLinked(maintenance.LinkedDeviations, x => x.Deviation != null ? x.Deviation.DeviationId + " — " + x.Deviation.Title : null, maintenance.Deviation != null ? maintenance.Deviation.DeviationId + " — " + maintenance.Deviation.Title : null))},{CsvSafe(JoinLinked(maintenance.LinkedCapas, x => x.Capa != null ? x.Capa.CapaId + " — " + x.Capa.Title : null, maintenance.Capa != null ? maintenance.Capa.CapaId + " — " + maintenance.Capa.Title : null))},{CsvSafe(maintenance.Description)},{maintenance.ScheduledDate.ToShortDateString()},{CsvSafe(maintenance.ScheduledBy)},{CsvSafe(maintenance.PerformedBy)},{maintenance.PerformedOn.ToSite()?.ToShortDateString() ?? ""},{CsvSafe(maintenance.WorkPerformed)},{CsvSafe(maintenance.PartsUsed)},{maintenance.Cost?.ToString("F2") ?? ""},{CsvSafe(maintenance.ReviewComments)},{CsvSafe(maintenance.ReviewedBy)},{maintenance.ReviewedOn.ToSite()?.ToShortDateString() ?? ""}");

            return File(CsvBytes(builder.ToString()), "text/csv", $"Maintenance-{maintenance.MaintenanceId}.csv");
        }

        private bool MaintenanceExists(int id) =>
            _context.Maintenance.Any(e => e.Id == id);

        private static string CsvSafe(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                return $"'{value}";
            return value;
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AdminReopen(int id, string reason, string email, string pwd)
        {
            var maintenance = await _context.Maintenance.FindAsync(id);
            if (maintenance == null) return NotFound();

            // Reviewed is the final state of the workflow; Approved is accepted too, for records from older versions.
            if (maintenance.Status != Maintenance.MaintenanceStatus.Reviewed
                && maintenance.Status != Maintenance.MaintenanceStatus.Approved)
            {
                TempData["WorkflowError"] = this.T("This record is not in a state that can be administratively reopened.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = this.T("Electronic signature must use your own credentials.");
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = this.T("Invalid email or password.");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = this.T("A justification reason is required.");
                return RedirectToAction(nameof(Details), new { id });
            }

            await ReturnToCompletedAsync(maintenance, "Administrative Reopen", reason, "administratively reopened");
            await _context.SaveChangesAsync();

            TempData["Success"] = this.T("Record has been administratively reopened to {0} stage.{1}",
                this.T(EnumHelper.Display(Maintenance.MaintenanceStatus.Completed)), "");
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
