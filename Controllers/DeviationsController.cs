/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2024  C-realize IT Services SRL (https://www.c-realize.com)

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://www.c-realize.com/contact.  For AGPL licensing, see below.

AGPL:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see http://www.gnu.org/licenses/.
*/

using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.ViewModels;
using OpenQMS.Services;
using static OpenQMS.Models.Deviation;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class DeviationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public DeviationsController(
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
            ViewData["Products"] = _context.Product
                .Where(x => !x.IsDeleted && x.Status == Product.ProductStatus.Approved)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewData["Processes"] = _context.Process
                .Where(x => !x.IsDeleted && x.Status == Process.ProcessStatus.Approved)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewData["Assets"] = _context.Asset
                .Where(x => !x.IsDeleted && x.Status == Asset.AssetStatus.Approved)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewData["Materials"] = _context.Material
                .Where(x => !x.IsDeleted && x.Status == Material.MaterialStatus.Approved)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewData["Suppliers"] = _context.Supplier
                .Where(x => !x.IsDeleted && (x.Status == SupplierStatus.Approved || x.Status == SupplierStatus.Qualified || x.Status == SupplierStatus.PendingSuspension || x.Status == SupplierStatus.Suspended))
                .OrderBy(x => x.LegalName)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.LegalName })
                .ToList();
        }

        private async Task SyncDeviationJunctions(int deviationId,
            int[] productIds, int[] processIds, int[] assetIds,
            int[] materialIds, int[] supplierIds)
        {
            _context.DeviationProduct.RemoveRange(await _context.DeviationProduct.Where(x => x.DeviationId == deviationId).ToListAsync());
            _context.DeviationProcess.RemoveRange(await _context.DeviationProcess.Where(x => x.DeviationId == deviationId).ToListAsync());
            _context.DeviationAsset.RemoveRange(await _context.DeviationAsset.Where(x => x.DeviationId == deviationId).ToListAsync());
            _context.DeviationMaterial.RemoveRange(await _context.DeviationMaterial.Where(x => x.DeviationId == deviationId).ToListAsync());
            _context.DeviationSupplier.RemoveRange(await _context.DeviationSupplier.Where(x => x.DeviationId == deviationId).ToListAsync());
            foreach (var id in productIds.Distinct())  _context.DeviationProduct.Add(new DeviationProduct  { DeviationId = deviationId, ProductId  = id });
            foreach (var id in processIds.Distinct())  _context.DeviationProcess.Add(new DeviationProcess  { DeviationId = deviationId, ProcessId  = id });
            foreach (var id in assetIds.Distinct())    _context.DeviationAsset.Add(new DeviationAsset      { DeviationId = deviationId, AssetId    = id });
            foreach (var id in materialIds.Distinct()) _context.DeviationMaterial.Add(new DeviationMaterial { DeviationId = deviationId, MaterialId = id });
            foreach (var id in supplierIds.Distinct()) _context.DeviationSupplier.Add(new DeviationSupplier { DeviationId = deviationId, SupplierId = id });
        }

        private static string JoinLinked<T>(ICollection<T>? junctions, Func<T, string?> getName, string? legacyFallback)
        {
            if (junctions?.Any() == true)
                return string.Join("; ", junctions.Select(getName).Where(n => !string.IsNullOrEmpty(n)));
            return legacyFallback ?? "";
        }

        private string GenerateDeviationId()
        {
            var allIds = _context.Deviation
                .AsEnumerable()
                .Select(x =>
                {
                    if (!string.IsNullOrEmpty(x.DeviationId))
                    {
                        var parts = x.DeviationId.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var n)) return n;
                    }
                    return 0;
                });
            var next = (allIds.Any() ? allIds.Max() : 0) + 1;
            return $"DEV-{next.ToString().PadLeft(4, '0')}";
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.DeviationAuditLog.Add(new DeviationAuditLog
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

        // GET: Deviations/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var deviations = await _context.Deviation
                .Include(d => d.AuditLogs)
                .ToListAsync();

            ViewBag.Total = deviations.Count;
            // Explicit workflow order — Enum.GetValues sorts by integer value which puts PendingAcceptance last
            var workflowOrder = new[]
            {
                DeviationStatus.Identification,
                DeviationStatus.Evaluation,
                DeviationStatus.PendingAcceptance,
                DeviationStatus.Accepted,
                DeviationStatus.Resolution,
                DeviationStatus.Approved
            };
            ViewBag.StatusCounts = workflowOrder
                .ToDictionary(s => s == DeviationStatus.PendingAcceptance ? "Pending Acceptance" : s.ToString(),
                              s => deviations.Count(d => d.Status == s));
            ViewBag.OpenCount = deviations.Count(d => d.Status != DeviationStatus.Approved);
            ViewBag.ApprovedCount = deviations.Count(d => d.Status == DeviationStatus.Approved);

            ViewBag.PendingAcceptance = deviations
                .Where(d => d.Status == DeviationStatus.PendingAcceptance)
                .OrderBy(d => d.IdentifiedOn)
                .Take(10).ToList();

            ViewBag.PendingApproval = deviations
                .Where(d => d.Status == DeviationStatus.Resolution)
                .OrderBy(d => d.IdentifiedOn)
                .Take(10).ToList();

            // Aged open deviations (>30 days since identified, not approved)
            var cutoff = SiteTime.Today.AddDays(-30);
            ViewBag.Aged = deviations
                .Where(d => d.Status != DeviationStatus.Approved && d.IdentifiedOn.Date < cutoff)
                .OrderBy(d => d.IdentifiedOn)
                .Take(10).ToList();

            // Overdue open deviations (past their target resolution date, not approved)
            ViewBag.OverdueCount = deviations.Count(d => d.Status != DeviationStatus.Approved
                && d.TargetResolutionDate.HasValue && d.TargetResolutionDate.Value.Date < SiteTime.Today);
            ViewBag.Overdue = deviations
                .Where(d => d.Status != DeviationStatus.Approved
                    && d.TargetResolutionDate.HasValue && d.TargetResolutionDate.Value.Date < SiteTime.Today)
                .OrderBy(d => d.TargetResolutionDate)
                .Take(10).ToList();

            ViewBag.RecentActivity = deviations
                .Where(d => d.AuditLogs != null)
                .SelectMany(d => d.AuditLogs!.Select(l => new { Log = l, Deviation = d }))
                .OrderByDescending(x => x.Log.PerformedOn)
                .Take(12)
                .Select(x => new { x.Log, DeviationId = x.Deviation.DeviationId, RecordId = x.Deviation.Id, Title = x.Deviation.Title })
                .ToList();

            return View();
        }

        // GET: Deviations
        public async Task<IActionResult> Index()
        {
            if (_context.Deviation == null)
                return Problem("Entity set 'ApplicationDbContext.Deviation'  is null.");

            var deviations = await _context.Deviation
                .Where(d => !d.IsDeleted)
                .Include(d => d.LinkedProducts).ThenInclude(x => x.Product)
                .Include(d => d.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(d => d.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(d => d.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(d => d.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(d => d.Product)
                .Include(d => d.Process)
                .Include(d => d.Asset)
                .Include(d => d.Material)
                .Include(d => d.Supplier)
                .ToListAsync();

            return View(deviations);
        }

        // GET: Deviations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Deviation == null)
                return NotFound();

            var deviation = await _context.Deviation
                .Include(d => d.LinkedProducts).ThenInclude(x => x.Product)
                .Include(d => d.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(d => d.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(d => d.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(d => d.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(d => d.LinkedCapas).ThenInclude(x => x.Capa)
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(d => d.Attachments)
                .Include(d => d.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (deviation == null)
                return NotFound();

            // Fallback for records created before junction migration
            if (deviation.LinkedCapas?.Any() != true && deviation.LinkedCapaId.HasValue)
                ViewBag.LinkedCapa = await _context.Capa.FindAsync(deviation.LinkedCapaId.Value);

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.DeviationAttachment.DeletedOnly().Where(f => f.DeviationId == id).ToListAsync();
            return View(deviation);
        }

        // GET: Deviations/Create
        public IActionResult Create()
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole)
                && !User.IsInRole(Constants.OperationsUserRole)
                && !User.IsInRole(Constants.CommercialUserRole))
                return Forbid();

            PopulateViewData();
            return View();
        }

        // POST: Deviations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Title,EventType,Severity,DeviationId,Identification,IdentifiedBy")] Deviation deviation,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole)
                && !User.IsInRole(Constants.OperationsUserRole)
                && !User.IsInRole(Constants.CommercialUserRole))
                return Forbid();

            try
            {
                if (ModelState.IsValid)
                {
                    deviation.DeviationId = GenerateDeviationId();
                    deviation.IdentifiedBy = _userManager.GetUserName(User);
                    deviation.IdentifiedOn = DateTime.UtcNow;
                    // Assign the target resolution date from the configured deviation resolution period —
                    // activates DefaultDeviationResolutionDays (measured from the identified date).
                    var settings = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
                    deviation.TargetResolutionDate = deviation.IdentifiedOn.AddDays(settings.DefaultDeviationResolutionDays);
                    deviation.Status = DeviationStatus.Identification;

                    _context.Add(deviation);
                    await _context.SaveChangesAsync();

                    await SyncDeviationJunctions(deviation.Id,
                        selectedProducts  ?? Array.Empty<int>(),
                        selectedProcesses ?? Array.Empty<int>(),
                        selectedAssets    ?? Array.Empty<int>(),
                        selectedMaterials ?? Array.Empty<int>(),
                        selectedSuppliers ?? Array.Empty<int>());

                    AddAuditLog(deviation.Id, "Created", null, DeviationStatus.Identification.ToString(),
                        $"ID: {deviation.DeviationId}; Title: {deviation.Title}; Event Type: {deviation.EventType}; Identified by: {deviation.IdentifiedBy}");
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Deviation {deviation.DeviationId} created successfully.";
                    return RedirectToAction(nameof(Details), new { id = deviation.Id });
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
            }

            PopulateViewData();
            ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
            ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
            ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
            ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
            ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            return View(deviation);
        }

        // GET: Deviations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Deviation == null)
                return NotFound();

            if (User.IsInRole(Constants.ReadOnlyRole))
                return Forbid();

            var deviation = await _context.Deviation
                .Include(d => d.LinkedProducts).ThenInclude(x => x.Product)
                .Include(d => d.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(d => d.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(d => d.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(d => d.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(d => d.Product)
                .Include(d => d.Process)
                .Include(d => d.Asset)
                .Include(d => d.Material)
                .Include(d => d.Supplier)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deviation == null)
                return NotFound();

            if (deviation.Status == DeviationStatus.Approved
                || deviation.Status == DeviationStatus.PendingAcceptance
                || deviation.Status == DeviationStatus.Resolution)
                return Forbid();

            ViewBag.SelectedProductIds = deviation.LinkedProducts?.Any() == true
                ? deviation.LinkedProducts.Select(x => x.ProductId).ToList()
                : (deviation.ProductId.HasValue ? new List<int> { deviation.ProductId.Value } : new List<int>());
            ViewBag.SelectedProcessIds = deviation.LinkedProcesses?.Any() == true
                ? deviation.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (deviation.ProcessId.HasValue ? new List<int> { deviation.ProcessId.Value } : new List<int>());
            ViewBag.SelectedAssetIds = deviation.LinkedAssets?.Any() == true
                ? deviation.LinkedAssets.Select(x => x.AssetId).ToList()
                : (deviation.AssetId.HasValue ? new List<int> { deviation.AssetId.Value } : new List<int>());
            ViewBag.SelectedMaterialIds = deviation.LinkedMaterials?.Any() == true
                ? deviation.LinkedMaterials.Select(x => x.MaterialId).ToList()
                : (deviation.MaterialId.HasValue ? new List<int> { deviation.MaterialId.Value } : new List<int>());
            ViewBag.SelectedSupplierIds = deviation.LinkedSuppliers?.Any() == true
                ? deviation.LinkedSuppliers.Select(x => x.SupplierId).ToList()
                : (deviation.SupplierId.HasValue ? new List<int> { deviation.SupplierId.Value } : new List<int>());

            PopulateViewData();
            return View(deviation);
        }

        // POST: Deviations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            if (User.IsInRole(Constants.ReadOnlyRole))
                return Forbid();

            var snap = await _context.Deviation.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            var deviation = await _context.Deviation
                .Include(d => d.LinkedProducts)
                .Include(d => d.LinkedProcesses)
                .Include(d => d.LinkedAssets)
                .Include(d => d.LinkedMaterials)
                .Include(d => d.LinkedSuppliers)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deviation == null) return NotFound();

            // Block editing at stages where the record is locked
            if (deviation.Status == DeviationStatus.PendingAcceptance
                || deviation.Status == DeviationStatus.Resolution
                || deviation.Status == DeviationStatus.Approved)
                return Forbid();

            // Accepted stage edits restricted to quality roles
            if (deviation.Status == DeviationStatus.Accepted
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            // Capture current affected-item links before any change (for the audit diff)
            var oldProductIds  = deviation.LinkedProducts?.Select(x => x.ProductId).ToList()   ?? new List<int>();
            var oldProcessIds  = deviation.LinkedProcesses?.Select(x => x.ProcessId).ToList()  ?? new List<int>();
            var oldAssetIds    = deviation.LinkedAssets?.Select(x => x.AssetId).ToList()       ?? new List<int>();
            var oldMaterialIds = deviation.LinkedMaterials?.Select(x => x.MaterialId).ToList() ?? new List<int>();
            var oldSupplierIds = deviation.LinkedSuppliers?.Select(x => x.SupplierId).ToList() ?? new List<int>();

            // Apply only the fields that are editable at the current stage
            bool updated;
            switch (deviation.Status)
            {
                case DeviationStatus.Identification:
                    updated = await TryUpdateModelAsync(deviation, "",
                        d => d.Title, d => d.EventType, d => d.Severity, d => d.Identification);
                    break;
                case DeviationStatus.Evaluation:
                    updated = await TryUpdateModelAsync(deviation, "",
                        d => d.Evaluation, d => d.Investigation,
                        d => d.RootCauseCategory, d => d.CorrectiveAction);
                    break;
                case DeviationStatus.Accepted:
                    updated = await TryUpdateModelAsync(deviation, "",
                        d => d.Resolution);
                    break;
                default:
                    return Forbid();
            }

            if (!ModelState.IsValid)
            {
                PopulateViewData();
                ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
                ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
                ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
                ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
                ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
                return View(deviation);
            }

            if (deviation.Status == DeviationStatus.Identification)
            {
                // Clear old single FKs — migrated to junction tables
                deviation.ProductId  = null;
                deviation.ProcessId  = null;
                deviation.AssetId    = null;
                deviation.MaterialId = null;
                deviation.SupplierId = null;

                await SyncDeviationJunctions(deviation.Id,
                    selectedProducts  ?? Array.Empty<int>(),
                    selectedProcesses ?? Array.Empty<int>(),
                    selectedAssets    ?? Array.Empty<int>(),
                    selectedMaterials ?? Array.Empty<int>(),
                    selectedSuppliers ?? Array.Empty<int>());
            }

            deviation.ExportFilePath = string.Empty;

            var diff = new List<string>();
            if (snap != null)
            {
                AuditDiffHelper.Track(diff,     "Title",             snap.Title,           deviation.Title);
                AuditDiffHelper.TrackEnum(diff, "Type",              snap.EventType,        deviation.EventType);
                AuditDiffHelper.TrackEnum(diff, "Severity",          snap.Severity,         deviation.Severity);
                AuditDiffHelper.Track(diff,     "Identification",    snap.Identification,   deviation.Identification);
                AuditDiffHelper.Track(diff,     "Evaluation",        snap.Evaluation,       deviation.Evaluation);
                AuditDiffHelper.Track(diff,     "Investigation",     snap.Investigation,    deviation.Investigation);
                AuditDiffHelper.Track(diff,     "Root Cause",        snap.RootCause,        deviation.RootCause);
                AuditDiffHelper.TrackEnum(diff, "Root Cause Category", snap.RootCauseCategory, deviation.RootCauseCategory);
                AuditDiffHelper.Track(diff,     "Corrective Action", snap.CorrectiveAction, deviation.CorrectiveAction);
                AuditDiffHelper.Track(diff,     "Resolution",        snap.Resolution,       deviation.Resolution);
            }
            // Affected-item links are editable at the Identification stage — record add/remove in the diff.
            if (deviation.Status == DeviationStatus.Identification)
            {
                AuditDiffHelper.TrackSet(diff, "Affected Products",  oldProductIds,  selectedProducts  ?? Array.Empty<int>(),
                    ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Processes", oldProcessIds,  selectedProcesses ?? Array.Empty<int>(),
                    ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Assets",    oldAssetIds,    selectedAssets    ?? Array.Empty<int>(),
                    ids => _context.Asset.Where(a => ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Materials", oldMaterialIds, selectedMaterials ?? Array.Empty<int>(),
                    ids => _context.Material.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Suppliers", oldSupplierIds, selectedSuppliers ?? Array.Empty<int>(),
                    ids => _context.Supplier.Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id, s => s.LegalName));
            }
            if (diff.Count > 0) AddAuditLog(deviation.Id, "Edited", null, null, AuditDiffHelper.Build(diff));

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DeviationExists(deviation.Id))
                    return NotFound();
                throw;
            }

            TempData["Success"] = "Deviation updated successfully.";
            return RedirectToAction(nameof(Details), new { id = deviation.Id });
        }

        // POST: Deviations/SubmitForEvaluation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForEvaluation(int id, string email, string pwd, string? notes)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null) return NotFound();

            if (deviation.Status != DeviationStatus.Identification)
            {
                TempData["WorkflowError"] = "Deviation must be in Identification status to submit for evaluation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (deviation.EventType == null)
            {
                TempData["WorkflowError"] = "Event Type must be selected before submitting for evaluation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(deviation.Identification))
            {
                TempData["WorkflowError"] = "Identification description must be completed before submitting for evaluation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = deviation.Status.ToString();
            deviation.Status = DeviationStatus.Evaluation;
            deviation.ExportFilePath = string.Empty;

            _context.Update(deviation);
            AddAuditLog(deviation.Id, "Submitted for Evaluation", fromStatus, DeviationStatus.Evaluation.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deviation {deviation.DeviationId} submitted for evaluation.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Deviations/SubmitEvaluation  (Evaluation → PendingAcceptance)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitEvaluation(int id, string email, string pwd, string? notes)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null) return NotFound();

            if (deviation.Status != DeviationStatus.Evaluation)
            {
                TempData["WorkflowError"] = "Deviation must be in Evaluation status to submit the evaluation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(deviation.Evaluation))
            {
                TempData["WorkflowError"] = "Evaluation must be completed before submitting.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(deviation.Investigation))
            {
                TempData["WorkflowError"] = "Investigation must be completed before submitting.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (deviation.RootCauseCategory == null)
            {
                TempData["WorkflowError"] = "Root Cause Category must be selected before submitting.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(deviation.CorrectiveAction))
            {
                TempData["WorkflowError"] = "Corrective and Preventive Action must be completed before submitting.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = deviation.Status.ToString();
            var userName   = _userManager.GetUserName(User);
            deviation.Status          = DeviationStatus.PendingAcceptance;
            deviation.EvaluatedBy     = userName;
            deviation.EvaluatedOn     = DateTime.UtcNow;
            deviation.InvestigatedBy  = userName;
            deviation.InvestigatedOn  = DateTime.UtcNow;
            deviation.RootCauseBy     = userName;
            deviation.RootCauseOn     = DateTime.UtcNow;
            deviation.ActionBy        = userName;
            deviation.ActionOn        = DateTime.UtcNow;
            deviation.ExportFilePath  = string.Empty;

            _context.Update(deviation);
            AddAuditLog(deviation.Id, "Evaluation Submitted", fromStatus, DeviationStatus.PendingAcceptance.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deviation {deviation.DeviationId} evaluation submitted by {userName} — awaiting QA Manager acceptance.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Deviations/SubmitForResolution
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForResolution(int id, string email, string pwd, string? notes)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null) return NotFound();

            if (deviation.Status != DeviationStatus.Accepted)
            {
                TempData["WorkflowError"] = "Deviation must be in Accepted status to submit for approval.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(deviation.Resolution))
            {
                TempData["WorkflowError"] = "Resolution must be documented before submitting for approval.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus   = deviation.Status.ToString();
            var userName     = _userManager.GetUserName(User);
            deviation.Status      = DeviationStatus.Resolution;
            deviation.ResolvedBy  = userName;
            deviation.ResolvedOn  = DateTime.UtcNow;
            deviation.ExportFilePath = string.Empty;

            _context.Update(deviation);
            AddAuditLog(deviation.Id, "Submitted for Approval", fromStatus, DeviationStatus.Resolution.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deviation {deviation.DeviationId} submitted for approval by {userName}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Deviations/AcceptDeviation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptDeviation(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null)
                return NotFound();

            if (deviation.Status != DeviationStatus.PendingAcceptance)
            {
                TempData["WorkflowError"] = "Deviation must be in Pending Acceptance status to accept.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = deviation.Status.ToString();
            deviation.Status     = DeviationStatus.Accepted;
            deviation.AcceptedBy = _userManager.GetUserName(User);
            deviation.AcceptedOn = DateTime.UtcNow;
            deviation.ExportFilePath = string.Empty;

            _context.Update(deviation);
            AddAuditLog(deviation.Id, "Accepted", fromStatus, DeviationStatus.Accepted.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deviation {deviation.DeviationId} accepted by {deviation.AcceptedBy}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Deviations/ReturnDeviation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnDeviation(int id, string email, string pwd, string reason)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = "A reason is required to return a record.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null) return NotFound();

            if (deviation.Status != DeviationStatus.Evaluation
                && deviation.Status != DeviationStatus.PendingAcceptance
                && deviation.Status != DeviationStatus.Accepted
                && deviation.Status != DeviationStatus.Resolution)
            {
                TempData["WorkflowError"] = "Record cannot be returned from its current status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = deviation.Status;
            DeviationStatus toStatus;
            switch (fromStatus)
            {
                case DeviationStatus.Evaluation:
                    toStatus = DeviationStatus.Identification;
                    deviation.EvaluatedBy = null; deviation.EvaluatedOn = null;
                    break;
                case DeviationStatus.PendingAcceptance:
                    // Return to Evaluation so QU can rework the evaluation package
                    toStatus = DeviationStatus.Evaluation;
                    deviation.EvaluatedBy    = null; deviation.EvaluatedOn    = null;
                    deviation.InvestigatedBy = null; deviation.InvestigatedOn = null;
                    deviation.RootCauseBy    = null; deviation.RootCauseOn    = null;
                    deviation.ActionBy       = null; deviation.ActionOn       = null;
                    break;
                case DeviationStatus.Accepted:
                    // Return to Evaluation for full rework of evaluation package
                    toStatus = DeviationStatus.Evaluation;
                    deviation.AcceptedBy     = null; deviation.AcceptedOn     = null;
                    deviation.EvaluatedBy    = null; deviation.EvaluatedOn    = null;
                    deviation.InvestigatedBy = null; deviation.InvestigatedOn = null;
                    deviation.RootCauseBy    = null; deviation.RootCauseOn    = null;
                    deviation.ActionBy       = null; deviation.ActionOn       = null;
                    break;
                case DeviationStatus.Resolution:
                    toStatus = DeviationStatus.Accepted;
                    deviation.ResolvedBy = null; deviation.ResolvedOn = null;
                    break;
                default:
                    return Forbid();
            }

            deviation.Status = toStatus;
            deviation.ExportFilePath = string.Empty;
            _context.Update(deviation);
            AddAuditLog(deviation.Id, $"Returned to {toStatus}", fromStatus.ToString(), toStatus.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deviation {deviation.DeviationId} returned to {toStatus} status.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Deviations/ApproveDeviation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveDeviation(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null)
                return NotFound();

            if (deviation.Status != DeviationStatus.Resolution)
            {
                TempData["WorkflowError"] = "Deviation must be in Resolution status to approve.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(deviation.Resolution))
            {
                TempData["WorkflowError"] = "Resolution must be documented before approving the deviation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = deviation.Status.ToString();
            deviation.Status = DeviationStatus.Approved;
            deviation.ApprovedBy = _userManager.GetUserName(User);
            deviation.ApprovedOn = DateTime.UtcNow;
            deviation.ExportFilePath = string.Empty;
            var _devSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            deviation.RetainUntil = deviation.ApprovedOn.Value.AddYears(_devSets.DeviationRetentionYears);

            _context.Update(deviation);
            AddAuditLog(deviation.Id, "Approved", fromStatus, DeviationStatus.Approved.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deviation {deviation.DeviationId} approved by {deviation.ApprovedBy}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Deviations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Deviation == null)
                return NotFound();

            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            var deviation = await _context.Deviation
                .Include(d => d.LinkedProducts).ThenInclude(x => x.Product)
                .Include(d => d.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(d => d.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(d => d.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(d => d.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(d => d.Capas)
                .Include(d => d.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (deviation == null)
                return NotFound();

            if (deviation.Status == DeviationStatus.PendingAcceptance || deviation.Status == DeviationStatus.Accepted
                || deviation.Status == DeviationStatus.Resolution || deviation.Status == DeviationStatus.Approved)
                return Forbid();

            return View(deviation);
        }

        // POST: Deviations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            if (_context.Deviation == null)
                return Problem("Entity set 'ApplicationDbContext.Deviation'  is null.");

            var deviation = await _context.Deviation
                .Include(d => d.Attachments)
                .Include(d => d.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (deviation == null)
                return NotFound();

            if (deviation.Status == DeviationStatus.PendingAcceptance || deviation.Status == DeviationStatus.Accepted
                || deviation.Status == DeviationStatus.Resolution || deviation.Status == DeviationStatus.Approved)
                return Forbid();

            // Attachments (rows and files) are retained with the soft-deleted record.

            AddAuditLog(deviation.Id, "Deleted", deviation.Status.ToString(), null, deviation.Title);
            deviation.IsDeleted = true;
            deviation.DeletedBy = _userManager.GetUserName(User);
            deviation.DeletedOn = DateTime.UtcNow;
            _context.Deviation.Update(deviation);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Deviations/ExportRegister
        public async Task<IActionResult> ExportRegister()
        {
            var deviations = await _context.Deviation
                .Include(d => d.LinkedProducts).ThenInclude(x => x.Product)
                .Include(d => d.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(d => d.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(d => d.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(d => d.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(d => d.Product).Include(d => d.Process)
                .Include(d => d.Asset).Include(d => d.Material).Include(d => d.Supplier)
                .OrderByDescending(d => d.IdentifiedOn)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Deviations");

            var headers = new[]
            {
                "Deviation ID", "Title", "Status", "Severity", "Products", "Processes", "Assets", "Materials", "Suppliers",
                "Identification", "Identified By", "Identified On",
                "Evaluation", "Evaluated By", "Evaluated On",
                "Investigation", "Root Cause", "Root Cause Category", "Corrective Action",
                "Accepted By", "Accepted On",
                "Resolution", "Resolved By", "Resolved On",
                "Approved By", "Approved On"
            };

            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            headerRow.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var d in deviations)
            {
                ws.Cell(row, 1).Value = d.DeviationId;
                ws.Cell(row, 2).Value = d.Title;
                ws.Cell(row, 3).Value = d.Status.ToString();
                ws.Cell(row, 4).Value = d.Severity?.ToString() ?? "";
                ws.Cell(row, 5).Value = JoinLinked(d.LinkedProducts,  x => x.Product?.Name,     d.Product?.Name);
                ws.Cell(row, 6).Value = JoinLinked(d.LinkedProcesses, x => x.Process?.Name,    d.Process?.Name);
                ws.Cell(row, 7).Value = JoinLinked(d.LinkedAssets,    x => x.Asset?.Name,      d.Asset?.Name);
                ws.Cell(row, 8).Value = JoinLinked(d.LinkedMaterials, x => x.Material?.Name,   d.Material?.Name);
                ws.Cell(row, 9).Value = JoinLinked(d.LinkedSuppliers, x => x.Supplier?.LegalName, d.Supplier?.LegalName);
                ws.Cell(row, 10).Value = d.Identification;
                ws.Cell(row, 11).Value = d.IdentifiedBy;
                ws.Cell(row, 12).Value = d.IdentifiedOn.ToSite().ToShortDateString();
                ws.Cell(row, 13).Value = d.Evaluation ?? "";
                ws.Cell(row, 14).Value = d.EvaluatedBy ?? "";
                ws.Cell(row, 15).Value = d.EvaluatedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 16).Value = d.Investigation ?? "";
                ws.Cell(row, 17).Value = d.RootCause ?? "";
                ws.Cell(row, 18).Value = d.RootCauseCategory?.ToString() ?? "";
                ws.Cell(row, 19).Value = d.CorrectiveAction ?? "";
                ws.Cell(row, 20).Value = d.AcceptedBy ?? "";
                ws.Cell(row, 21).Value = d.AcceptedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 22).Value = d.Resolution ?? "";
                ws.Cell(row, 23).Value = d.ResolvedBy ?? "";
                ws.Cell(row, 24).Value = d.ResolvedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 25).Value = d.ApprovedBy ?? "";
                ws.Cell(row, 26).Value = d.ApprovedOn.ToSite()?.ToShortDateString() ?? "";

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                row++;
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            OpenQMS.Services.ExcelExportHelper.AddExportInfoSheet(wb, _userManager.GetUserName(User) ?? "Unknown", "Deviations Register", deviations.Count);

            // Register exports are recorded in the workbook's Export Info sheet (who / when / count).
            // No per-record audit entry: there is no parent record, and id 0 violates the audit-log FK.

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Deviations-Register-{SiteTime.Today:yyyy-MM-dd}.xlsx");
        }

        public async Task<IActionResult> ExportDeviationDetail(int id)
        {
            if (_context.Deviation == null) return NotFound();

            var deviation = await _context.Deviation
                .Where(x => x.Id == id)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.Product).Include(x => x.Process)
                .Include(x => x.Asset).Include(a => a.Material).Include(x => x.Supplier).Include(a => a.Capas)
                .FirstOrDefaultAsync();

            if (deviation == null) return NotFound();

            var capaList = deviation.Capas?.ToList() ?? new List<Capa>();

            var fields = new List<(string, string?)>
            {
                ("Deviation ID",    deviation.DeviationId),
                ("Title",           deviation.Title),
                ("Status",          deviation.Status.ToString()),
                ("Event Type",      deviation.EventType?.ToString()),
                ("Severity",        deviation.Severity?.ToString()),
                ("Products",        JoinLinked(deviation.LinkedProducts,  x => x.Product?.Name,     deviation.Product?.Name)),
                ("Processes",       JoinLinked(deviation.LinkedProcesses, x => x.Process?.Name,    deviation.Process?.Name)),
                ("Assets",          JoinLinked(deviation.LinkedAssets,    x => x.Asset?.Name,      deviation.Asset?.Name)),
                ("Materials",       JoinLinked(deviation.LinkedMaterials, x => x.Material?.Name,   deviation.Material?.Name)),
                ("Suppliers",       JoinLinked(deviation.LinkedSuppliers, x => x.Supplier?.LegalName, deviation.Supplier?.LegalName)),
                ("Identification",  deviation.Identification),
                ("Identified By",   deviation.IdentifiedBy),
                ("Identified On",   deviation.IdentifiedOn.ToSite().ToShortDateString()),
                ("Evaluation",      deviation.Evaluation),
                ("Evaluated By",    deviation.EvaluatedBy),
                ("Evaluated On",    deviation.EvaluatedOn.ToSite()?.ToShortDateString()),
                ("Accepted By",     deviation.AcceptedBy),
                ("Accepted On",     deviation.AcceptedOn.ToSite()?.ToShortDateString()),
                ("Resolution",      deviation.Resolution),
                ("Resolved By",     deviation.ResolvedBy),
                ("Resolved On",     deviation.ResolvedOn.ToSite()?.ToShortDateString()),
                ("Approved By",     deviation.ApprovedBy),
                ("Approved On",     deviation.ApprovedOn.ToSite()?.ToShortDateString()),
            };

            var tables = new List<(string, string[], IEnumerable<string[]>)>
            {
                ("Linked CAPAs",
                 new[] { "CAPA ID", "Title", "Status" },
                 capaList.Select(c => new[] { c.CapaId, c.Title, c.Status.ToString() })),
            };

            AddAuditLog(deviation.Id, "Exported (PDF)", deviation.Status.ToString(), deviation.Status.ToString());
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Deviation Detail Report", deviation.DeviationId, deviation.Status.ToString(), fields, tables,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Deviation-{deviation.DeviationId}.pdf");
        }

        public async Task<IActionResult> ExportDetailInCSV(int id)
        {
            if (_context.Deviation == null) return NotFound();
            var deviation = await _context.Deviation
                .Where(x => x.Id == id)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.Product).Include(x => x.Process)
                .Include(x => x.Asset).Include(x => x.Material).Include(x => x.Supplier)
                .FirstOrDefaultAsync();
            if (deviation == null) return NotFound();

            AddAuditLog(deviation.Id, "Exported (CSV)", deviation.Status.ToString(), deviation.Status.ToString());
            await _context.SaveChangesAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Deviation ID,Title,Event Type,Status,Severity,Products,Processes,Assets,Materials,Suppliers,Identification,Identified By,Identified On,Evaluation,Evaluated By,Evaluated On,Investigation,Root Cause,Root Cause Category,Corrective Action,Accepted By,Accepted On,Resolution,Resolved By,Resolved On,Approved By,Approved On");
            builder.AppendLine($"{CsvSafe(deviation.DeviationId)},{CsvSafe(deviation.Title)},{deviation.EventType?.ToString() ?? ""},{deviation.Status},{deviation.Severity?.ToString() ?? ""},{CsvSafe(JoinLinked(deviation.LinkedProducts, x => x.Product?.Name, deviation.Product?.Name))},{CsvSafe(JoinLinked(deviation.LinkedProcesses, x => x.Process?.Name, deviation.Process?.Name))},{CsvSafe(JoinLinked(deviation.LinkedAssets, x => x.Asset?.Name, deviation.Asset?.Name))},{CsvSafe(JoinLinked(deviation.LinkedMaterials, x => x.Material?.Name, deviation.Material?.Name))},{CsvSafe(JoinLinked(deviation.LinkedSuppliers, x => x.Supplier?.LegalName, deviation.Supplier?.LegalName))},{CsvSafe(deviation.Identification)},{CsvSafe(deviation.IdentifiedBy)},{deviation.IdentifiedOn.ToSite().ToShortDateString()},{CsvSafe(deviation.Evaluation)},{CsvSafe(deviation.EvaluatedBy)},{deviation.EvaluatedOn.ToSite()?.ToShortDateString() ?? ""},{CsvSafe(deviation.Investigation)},{CsvSafe(deviation.RootCause)},{deviation.RootCauseCategory?.ToString() ?? ""},{CsvSafe(deviation.CorrectiveAction)},{CsvSafe(deviation.AcceptedBy)},{deviation.AcceptedOn.ToSite()?.ToShortDateString() ?? ""},{CsvSafe(deviation.Resolution)},{CsvSafe(deviation.ResolvedBy)},{deviation.ResolvedOn.ToSite()?.ToShortDateString() ?? ""},{CsvSafe(deviation.ApprovedBy)},{deviation.ApprovedOn.ToSite()?.ToShortDateString() ?? ""}");

            return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"Deviation-{deviation.DeviationId}.csv");
        }

        public ActionResult GetDocuments()
        {
            if (_context.Deviation == null) return NotFound();
            var deviations = _context.Deviation.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(DeviationStatus)).Cast<DeviationStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();
            if (deviations != null && deviations.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    dataSet.Data.Add(deviations.Count(x => x.Status.Equals(label)));
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }
            dataSets.Add(dataSet);
            return Json(new { dataSet = dataSets, labels });
        }

        private bool DeviationExists(int id)
        {
            return (_context.Deviation?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private static string CsvSafe(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                return $"'{value}";
            return value;
        }

        // GET: Deviations/Attachments/5 (kept for backwards compatibility)
        public async Task<IActionResult> Attachments(int? id)
        {
            if (id == null) return NotFound();
            var deviation = await _context.Deviation
                .Include(d => d.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (deviation == null) return NotFound();
            return View(deviation);
        }

        // POST: Deviations/UploadAttachment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int deviationId, IFormFile file, string? description)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = deviationId });
            }

            var deviation = await _context.Deviation.FindAsync(deviationId);
            if (deviation == null) return NotFound();

            if (deviation.Status == Deviation.DeviationStatus.Approved)
            {
                TempData["Error"] = "Attachments cannot be added to an approved deviation.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = deviationId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, ZIP, JPG, PNG.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = deviationId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds 25 MB limit.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = deviationId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "deviations", deviationId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var safeName = Path.GetFileNameWithoutExtension(file.FileName).Replace(" ", "_").Replace("..", "");
            var uniqueFileName = $"{timestamp}_{safeName}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            _context.DeviationAttachment.Add(new DeviationAttachment
            {
                DeviationId = deviationId,
                FileName = file.FileName,
                FilePath = $"/uploads/deviations/{deviationId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                Description = description
            });

            AddAuditLog(deviationId, "Attachment uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded successfully.";
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = deviationId });
        }

        // GET: Deviations/DownloadAttachment/5
        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();
            var attachment = await _context.DeviationAttachment.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.DeviationId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.DeviationId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.DeviationId });
            }

            AddAuditLog(attachment.DeviationId, (deleted ? "Deleted attachment downloaded" : "Attachment downloaded"), null, null, attachment.FileName);
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Deviation",
                attachment.DeviationId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.DeviationAttachment.FindAsync(id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.DeviationId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.DeviationId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.DeviationId });
            }

            AddAuditLog(attachment.DeviationId, "Attachment viewed", null, null, attachment.FileName);
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Deviation",
                attachment.DeviationId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream");
        }

        // POST: Deviations/DeleteAttachment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int deviationId, string? deletionReason)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var attachment = await _context.DeviationAttachment.FindAsync(id);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                deviationId = attachment.DeviationId;

                var deviation = await _context.Deviation.FindAsync(deviationId);
                if (deviation != null && deviation.Status == Deviation.DeviationStatus.Approved)
                {
                    TempData["Error"] = "Attachments cannot be removed from an approved deviation.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = deviationId });
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = deviationId });
                }

                // Soft delete: the row and file are retained for traceability.
                attachment.IsDeleted      = true;
                attachment.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                AddAuditLog(deviationId, "Attachment deleted", null, null, $"{attachment.FileName} — Reason: {attachment.DeletionReason}");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attachment deleted successfully.";
            }
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = deviationId });
        }

        // POST: Create CAPA from Deviation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCapa(int? id)
        {
            if (id == null) return NotFound();
            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null) return NotFound();

            if (deviation.LinkedCapaId.HasValue)
            {
                TempData["Error"] = "A CAPA is already linked to this deviation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return RedirectToAction("Create", "Capas", new { deviationId = id });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AdminReopen(int id, string reason, string email, string pwd)
        {
            var deviation = await _context.Deviation.FindAsync(id);
            if (deviation == null) return NotFound();

            if (deviation.Status != DeviationStatus.Approved)
            {
                TempData["WorkflowError"] = "This record is not in a state that can be administratively reopened.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = "A justification reason is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = deviation.Status.ToString();
            deviation.Status = DeviationStatus.Resolution;
            AddAuditLog(deviation.Id, "Administrative Reopen", fromStatus, DeviationStatus.Resolution.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Record has been administratively reopened to Resolution stage.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
