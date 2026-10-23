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
using static OpenQMS.Models.Change;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class ChangesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public ChangesController(
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
            ViewData["Capas"] = _context.Capa
                .Where(x => !x.IsDeleted && x.Status == Capa.CapaStatus.Approved)
                .OrderBy(x => x.CapaId)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = $"{x.CapaId} - {x.Title}" })
                .ToList();
        }

        private async Task SyncChangeJunctions(int changeId,
            int[] productIds, int[] processIds, int[] assetIds,
            int[] materialIds, int[] supplierIds, int[] capaIds)
        {
            _context.ChangeProduct.RemoveRange(
                await _context.ChangeProduct.Where(x => x.ChangeId == changeId).ToListAsync());
            _context.ChangeProcess.RemoveRange(
                await _context.ChangeProcess.Where(x => x.ChangeId == changeId).ToListAsync());
            _context.ChangeAsset.RemoveRange(
                await _context.ChangeAsset.Where(x => x.ChangeId == changeId).ToListAsync());
            _context.ChangeMaterial.RemoveRange(
                await _context.ChangeMaterial.Where(x => x.ChangeId == changeId).ToListAsync());
            _context.ChangeSupplier.RemoveRange(
                await _context.ChangeSupplier.Where(x => x.ChangeId == changeId).ToListAsync());
            _context.ChangeCapa.RemoveRange(
                await _context.ChangeCapa.Where(x => x.ChangeId == changeId).ToListAsync());

            foreach (var id in productIds.Distinct())
                _context.ChangeProduct.Add(new ChangeProduct { ChangeId = changeId, ProductId = id });
            foreach (var id in processIds.Distinct())
                _context.ChangeProcess.Add(new ChangeProcess { ChangeId = changeId, ProcessId = id });
            foreach (var id in assetIds.Distinct())
                _context.ChangeAsset.Add(new ChangeAsset { ChangeId = changeId, AssetId = id });
            foreach (var id in materialIds.Distinct())
                _context.ChangeMaterial.Add(new ChangeMaterial { ChangeId = changeId, MaterialId = id });
            foreach (var id in supplierIds.Distinct())
                _context.ChangeSupplier.Add(new ChangeSupplier { ChangeId = changeId, SupplierId = id });
            foreach (var id in capaIds.Distinct())
                _context.ChangeCapa.Add(new ChangeCapa { ChangeId = changeId, CapaId = id });
        }

        private string GenerateChangeId()
        {
            var allIds = _context.Change
                .AsEnumerable()
                .Select(x =>
                {
                    if (!string.IsNullOrEmpty(x.ChangeId))
                    {
                        var parts = x.ChangeId.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var n)) return n;
                    }
                    return 0;
                });
            var next = (allIds.Any() ? allIds.Max() : 0) + 1;
            return $"CHG-{next.ToString().PadLeft(4, '0')}";
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.ChangeAuditLog.Add(new ChangeAuditLog
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

        // GET: Changes/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var changes = await _context.Change
                .Include(c => c.AuditLogs)
                .ToListAsync();

            var today = SiteTime.Today;

            var orderedStatuses = new[]
            {
                ChangeStatus.Proposal,
                ChangeStatus.Assessment,
                ChangeStatus.PendingAssessment,
                ChangeStatus.Implementation,
                ChangeStatus.PendingApproval,
                ChangeStatus.Approved
            };

            ViewBag.Total = changes.Count;
            ViewBag.StatusCounts = orderedStatuses.ToDictionary(
                s => s == ChangeStatus.PendingAssessment ? "Pending Acceptance"
                   : s == ChangeStatus.PendingApproval   ? "Pending Approval"
                   : s.ToString(),
                s => changes.Count(c => c.Status == s));
            ViewBag.OpenCount = changes.Count(c => c.Status != ChangeStatus.Approved);
            ViewBag.ApprovedCount = changes.Count(c => c.Status == ChangeStatus.Approved);

            ViewBag.PendingAcceptance = changes
                .Where(c => c.Status == ChangeStatus.PendingAssessment)
                .OrderBy(c => c.TargetImplementationDate)
                .Take(10)
                .ToList();

            ViewBag.PendingApproval = changes
                .Where(c => c.Status == ChangeStatus.PendingApproval)
                .OrderBy(c => c.TargetImplementationDate)
                .Take(10)
                .ToList();

            ViewBag.Overdue = changes
                .Where(c => c.Status != ChangeStatus.Approved
                         && c.TargetImplementationDate.HasValue
                         && c.TargetImplementationDate.Value.Date < today)
                .OrderBy(c => c.TargetImplementationDate)
                .Take(10)
                .ToList();

            ViewBag.Approaching = changes
                .Where(c => c.Status != ChangeStatus.Approved
                         && c.TargetImplementationDate.HasValue
                         && c.TargetImplementationDate.Value.Date >= today
                         && c.TargetImplementationDate.Value.Date <= today.AddDays(14))
                .OrderBy(c => c.TargetImplementationDate)
                .Take(10)
                .ToList();

            ViewBag.RecentActivity = changes
                .Where(c => c.AuditLogs != null)
                .SelectMany(c => c.AuditLogs!.Select(l => new { Log = l, Change = c }))
                .OrderByDescending(x => x.Log.PerformedOn)
                .Take(12)
                .Select(x => new { x.Log, ChangeId = x.Change.ChangeId, RecordId = x.Change.Id, Title = x.Change.Title })
                .ToList();

            return View();
        }

        // GET: Changes
        public async Task<IActionResult> Index()
        {
            if (_context.Change == null)
                return Problem("Entity set 'ApplicationDbContext.Change'  is null.");

            var changes = await _context.Change
                .Where(x => !x.IsDeleted)
                .Include(x => x.Product)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.LinkedCapas).ThenInclude(x => x.Capa)
                .ToListAsync();

            return View(changes);
        }

        // GET: Changes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Change == null)
                return NotFound();

            var change = await _context.Change
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Capa)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedCapas).ThenInclude(x => x.Capa)
                .Include(c => c.ActionPlanItems)
                .Include(c => c.Attachments)
                .Include(c => c.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (change == null)
                return NotFound();

            ViewBag.SystemUserNames = await _userManager.Users
                .Select(u => u.UserName!)
                .OrderBy(n => n)
                .ToListAsync();
            ViewBag.SuggestedActions = change.ChangeType.HasValue
                && ActionCatalogue.TryGetValue(change.ChangeType.Value, out var acts)
                ? acts : Array.Empty<string>();

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.ChangeAttachment.DeletedOnly().Where(f => f.ChangeId == id).ToListAsync();
            return View(change);
        }

        // Pre-implementation action catalogue — candidate actions per change type
        // (Change Control Management SOP, Appendix C). Built-in reference data: changes are a
        // software change under Change Control / CSV. Used to pre-populate the Action Plan tab.
        private static readonly Dictionary<ChangeTypeCategory, string[]> ActionCatalogue = new()
        {
            [ChangeTypeCategory.Supplier] = new[] { "Risk assessment", "Supplier qualification / approval", "Quality / Technical / GMP Agreement (SDEA) review or execution", "Material / specification verification", "Regulatory impact assessment (source change may need a variation)", "Update approved-supplier data", "Training" },
            [ChangeTypeCategory.Material] = new[] { "Risk assessment", "Update material specification", "Supplier confirmation", "Analytical method suitability", "Stability (if applicable)", "Process / formulation re-validation (if impacted)", "Document updates", "Regulatory impact assessment" },
            [ChangeTypeCategory.Product] = new[] { "Risk assessment", "(Re)validation", "Specification & document updates", "Stability", "Regulatory variation / approval", "Label / artwork / PI update", "Training" },
            [ChangeTypeCategory.Process] = new[] { "Risk assessment", "Process / cleaning validation (Annex 15)", "Equipment / asset impact assessment", "BMR / BPR & SOP updates", "Training", "Regulatory impact assessment" },
            [ChangeTypeCategory.Equipment] = new[] { "Risk assessment", "Qualification (IQ / OQ / PQ)", "Calibration / maintenance plan", "Cleaning-validation impact", "SOP updates", "Training" },
            [ChangeTypeCategory.AnalyticalMethod] = new[] { "Risk assessment", "Method validation / verification", "Specification impact", "Reference-standard impact", "SOP updates", "Training", "Regulatory impact assessment" },
            [ChangeTypeCategory.ComputerisedSystem] = new[] { "Risk assessment", "CSV (impact assessment, IQ / OQ, data-integrity & audit-trail check)", "Access / role review", "Backup / restore impact", "SOP updates", "Training" },
            [ChangeTypeCategory.Document] = new[] { "Risk assessment (if requirement changes)", "Document revision to Effective", "Training assignment", "Downstream reference updates" },
            [ChangeTypeCategory.Label] = new[] { "Risk assessment", "Artwork / PI update & proofing", "Regulatory variation / notification", "Stock / version reconciliation", "Document updates" },
            [ChangeTypeCategory.Regulatory] = new[] { "Impact assessment", "Implement the registered change", "Update licence / registration data", "Document & training updates", "Confirm condition met" },
            [ChangeTypeCategory.StorageDistribution] = new[] { "Risk assessment", "(Re)qualification of conditions", "Temperature-mapping / monitoring impact", "Agreement / supplier impact", "SOP updates", "Training" },
            [ChangeTypeCategory.Other] = Array.Empty<string>(),
        };

        // GET: Changes/Create
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

        // POST: Changes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Title,Proposal,TargetImplementationDate")] Change change,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers, int[]? selectedCapas)
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
                    change.ChangeId = GenerateChangeId();
                    change.ProposedBy = _userManager.GetUserName(User);
                    change.ProposedOn = DateTime.UtcNow;
                    change.Status = ChangeStatus.Proposal;

                    _context.Add(change);
                    await _context.SaveChangesAsync();

                    await SyncChangeJunctions(change.Id,
                        selectedProducts  ?? Array.Empty<int>(),
                        selectedProcesses ?? Array.Empty<int>(),
                        selectedAssets    ?? Array.Empty<int>(),
                        selectedMaterials ?? Array.Empty<int>(),
                        selectedSuppliers ?? Array.Empty<int>(),
                        selectedCapas     ?? Array.Empty<int>());

                    AddAuditLog(change.Id, "Created", null, ChangeStatus.Proposal.ToString(),
                        $"ID: {change.ChangeId}; Title: {change.Title}; Proposed by: {change.ProposedBy}");
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Change {change.ChangeId} created successfully.";
                    return RedirectToAction(nameof(Details), new { id = change.Id });
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            PopulateViewData();
            ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
            ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
            ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
            ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
            ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            ViewBag.SelectedCapaIds     = selectedCapas?.ToList()     ?? new List<int>();
            return View(change);
        }

        // GET: Changes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (User.IsInRole(Constants.ReadOnlyRole))
                return Forbid();

            if (id == null || _context.Change == null)
                return NotFound();

            var change = await _context.Change
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Capa)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedCapas).ThenInclude(x => x.Capa)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (change == null)
                return NotFound();

            if (change.Status == ChangeStatus.Approved ||
                change.Status == ChangeStatus.PendingAssessment ||
                change.Status == ChangeStatus.PendingApproval)
                return Forbid();

            PopulateViewData();

            // Pre-select junction data; fall back to old FK if no junction rows exist yet
            ViewBag.SelectedProductIds  = change.LinkedProducts?.Any()  == true
                ? change.LinkedProducts.Select(x => x.ProductId).ToList()
                : (change.ProductId.HasValue  ? new List<int> { change.ProductId.Value }  : new List<int>());
            ViewBag.SelectedProcessIds  = change.LinkedProcesses?.Any() == true
                ? change.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (change.ProcessId.HasValue  ? new List<int> { change.ProcessId.Value }  : new List<int>());
            ViewBag.SelectedAssetIds    = change.LinkedAssets?.Any()    == true
                ? change.LinkedAssets.Select(x => x.AssetId).ToList()
                : (change.AssetId.HasValue    ? new List<int> { change.AssetId.Value }    : new List<int>());
            ViewBag.SelectedMaterialIds = change.LinkedMaterials?.Any() == true
                ? change.LinkedMaterials.Select(x => x.MaterialId).ToList()
                : (change.MaterialId.HasValue ? new List<int> { change.MaterialId.Value } : new List<int>());
            ViewBag.SelectedSupplierIds = change.LinkedSuppliers?.Any() == true
                ? change.LinkedSuppliers.Select(x => x.SupplierId).ToList()
                : (change.SupplierId.HasValue ? new List<int> { change.SupplierId.Value } : new List<int>());
            ViewBag.SelectedCapaIds     = change.LinkedCapas?.Any()     == true
                ? change.LinkedCapas.Select(x => x.CapaId).ToList()
                : (change.CapaId.HasValue     ? new List<int> { change.CapaId.Value }     : new List<int>());

            return View(change);
        }

        // POST: Changes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormCollection form,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers, int[]? selectedCapas)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var snap   = await _context.Change.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            var change = await _context.Change.FirstOrDefaultAsync(c => c.Id == id);
            if (change == null || snap == null) return NotFound();

            if (change.Status == ChangeStatus.Approved ||
                change.Status == ChangeStatus.PendingAssessment ||
                change.Status == ChangeStatus.PendingApproval)
                return Forbid();

            var title          = form["Title"].FirstOrDefault() ?? change.Title;
            var proposal       = form["Proposal"].FirstOrDefault();
            var assessment     = form["Assessment"].FirstOrDefault();
            var implementation = form["Implementation"].FirstOrDefault();
            var targetDateStr  = form["TargetImplementationDate"].FirstOrDefault();
            DateTime? targetDate = DateTime.TryParse(targetDateStr, out var td) ? td : (DateTime?)null;

            var isProposalStage   = change.Status == ChangeStatus.Proposal;
            var isAssessmentStage = change.Status == ChangeStatus.Assessment;

            if (isProposalStage || isAssessmentStage)
            {
                if (string.IsNullOrWhiteSpace(title))
                    ModelState.AddModelError("Title", "Title is required.");
                if (string.IsNullOrWhiteSpace(proposal))
                    ModelState.AddModelError("Proposal", "Proposal is required.");
            }

            if (!ModelState.IsValid)
            {
                PopulateViewData();
                ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
                ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
                ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
                ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
                ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
                ViewBag.SelectedCapaIds     = selectedCapas?.ToList()     ?? new List<int>();
                return View(change);
            }

            // Capture affected-item / linked-CAPA associations before sync (for the audit diff)
            var oldChProductIds  = await _context.ChangeProduct.Where(x => x.ChangeId == change.Id).Select(x => x.ProductId).ToListAsync();
            var oldChProcessIds  = await _context.ChangeProcess.Where(x => x.ChangeId == change.Id).Select(x => x.ProcessId).ToListAsync();
            var oldChAssetIds    = await _context.ChangeAsset.Where(x => x.ChangeId == change.Id).Select(x => x.AssetId).ToListAsync();
            var oldChMaterialIds = await _context.ChangeMaterial.Where(x => x.ChangeId == change.Id).Select(x => x.MaterialId).ToListAsync();
            var oldChSupplierIds = await _context.ChangeSupplier.Where(x => x.ChangeId == change.Id).Select(x => x.SupplierId).ToListAsync();
            var oldChCapaIds     = await _context.ChangeCapa.Where(x => x.ChangeId == change.Id).Select(x => x.CapaId).ToListAsync();

            if (isProposalStage || isAssessmentStage)
            {
                change.Title                    = title;
                change.Proposal                 = proposal;
                change.TargetImplementationDate = targetDate;
                // Clear legacy single FK columns — junction tables are now authoritative
                change.ProductId  = null;
                change.ProcessId  = null;
                change.AssetId    = null;
                change.MaterialId = null;
                change.SupplierId = null;
                change.CapaId     = null;
                await SyncChangeJunctions(change.Id,
                    selectedProducts  ?? Array.Empty<int>(),
                    selectedProcesses ?? Array.Empty<int>(),
                    selectedAssets    ?? Array.Empty<int>(),
                    selectedMaterials ?? Array.Empty<int>(),
                    selectedSuppliers ?? Array.Empty<int>(),
                    selectedCapas     ?? Array.Empty<int>());
                if (isAssessmentStage)
                {
                    change.Assessment = string.IsNullOrWhiteSpace(assessment) ? null : assessment;

                    // Assessment-stage classification fields
                    change.RiskClassification = Enum.TryParse<ChangeRiskClassification>(form["RiskClassification"].FirstOrDefault(), out var rc) ? rc : (ChangeRiskClassification?)null;
                    change.ChangeType         = Enum.TryParse<ChangeTypeCategory>(form["ChangeType"].FirstOrDefault(), out var ct) ? ct : (ChangeTypeCategory?)null;
                    change.Nature             = Enum.TryParse<ChangeNature>(form["Nature"].FirstOrDefault(), out var nt) ? nt : (ChangeNature?)null;
                    change.DetailedActionPlanRequired = form["DetailedActionPlanRequired"].Any(v => v == "true");
                }
            }

            if (change.Status == ChangeStatus.Implementation)
                change.Implementation = string.IsNullOrWhiteSpace(implementation) ? null : implementation;

            change.ExportFilePath = string.Empty;

            var diff = new List<string>();
            AuditDiffHelper.Track(diff, "Title",               snap.Title,    change.Title);
            AuditDiffHelper.Track(diff, "Proposal",            snap.Proposal, change.Proposal);
            AuditDiffHelper.Track(diff, "Assessment",          snap.Assessment,   change.Assessment);
            AuditDiffHelper.TrackEnum(diff, "Risk Classification", snap.RiskClassification, change.RiskClassification);
            AuditDiffHelper.TrackEnum(diff, "Change Type",         snap.ChangeType,         change.ChangeType);
            AuditDiffHelper.TrackEnum(diff, "Nature",              snap.Nature,             change.Nature);
            AuditDiffHelper.Track<bool>(diff, "Detailed Action Plan Required", snap.DetailedActionPlanRequired, change.DetailedActionPlanRequired);
            AuditDiffHelper.Track(diff, "Implementation",      snap.Implementation, change.Implementation);
            AuditDiffHelper.TrackDate(diff, "Target Date",     snap.TargetImplementationDate, change.TargetImplementationDate);
            if (isProposalStage || isAssessmentStage)
            {
                AuditDiffHelper.TrackSet(diff, "Affected Products",  oldChProductIds,  selectedProducts  ?? Array.Empty<int>(),
                    ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Processes", oldChProcessIds,  selectedProcesses ?? Array.Empty<int>(),
                    ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Assets",    oldChAssetIds,    selectedAssets    ?? Array.Empty<int>(),
                    ids => _context.Asset.Where(a => ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Materials", oldChMaterialIds, selectedMaterials ?? Array.Empty<int>(),
                    ids => _context.Material.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Suppliers", oldChSupplierIds, selectedSuppliers ?? Array.Empty<int>(),
                    ids => _context.Supplier.Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id, s => s.LegalName));
                AuditDiffHelper.TrackSet(diff, "Linked CAPAs",       oldChCapaIds,     selectedCapas     ?? Array.Empty<int>(),
                    ids => _context.Capa.Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id, c => c.CapaId));
            }
            // Only record an "Edited" entry when a tracked field actually changed — every editable
            // field is tracked above, so an empty diff means nothing changed and a log line would be
            // misleading noise. Real edits still show the full field-level diff.
            if (diff.Count > 0)
                AddAuditLog(change.Id, "Edited", null, null, AuditDiffHelper.Build(diff));
            await _context.SaveChangesAsync();

            TempData["Success"] = "Change updated successfully.";
            return RedirectToAction(nameof(Details), new { id = change.Id });
        }

        // POST: Changes/SubmitForAssessment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForAssessment(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.QualityUserRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var change = await _context.Change.FindAsync(id);
            if (change == null) return NotFound();

            if (change.Status != ChangeStatus.Proposal)
            {
                TempData["WorkflowError"] = "Change must be in Proposal status to submit for assessment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(change.Proposal))
            {
                TempData["WorkflowError"] = "Proposal text must be completed before submitting for assessment.";
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

            var fromStatus = change.Status.ToString();
            change.Status = ChangeStatus.Assessment;
            change.ExportFilePath = string.Empty;

            _context.Update(change);
            AddAuditLog(change.Id, "Submitted for Assessment", fromStatus, ChangeStatus.Assessment.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Change {change.ChangeId} submitted for assessment.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Changes/SubmitForAcceptance
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForAcceptance(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.QualityUserRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var change = await _context.Change.FindAsync(id);
            if (change == null) return NotFound();

            if (change.Status != ChangeStatus.Assessment)
            {
                TempData["WorkflowError"] = "Change must be in Assessment status to submit for acceptance.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(change.Assessment))
            {
                TempData["WorkflowError"] = "Assessment must be completed before submitting for acceptance.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Action-plan readiness: each applicable item needs an owner and a target date before acceptance.
            var missingOwnerTarget = (await ApplicableActionItemsAsync(change))
                .Where(i => string.IsNullOrWhiteSpace(i.Owner) || i.TargetDate == null)
                .Select(i => Truncate(i.Action))
                .ToList();
            if (missingOwnerTarget.Count > 0)
            {
                TempData["WorkflowError"] = $"Each action needs an owner and a target date before submitting for acceptance — {missingOwnerTarget.Count} still incomplete: {string.Join("; ", missingOwnerTarget.Take(5))}{(missingOwnerTarget.Count > 5 ? "…" : "")}";
                TempData["ActiveTab"] = "actionplan";
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

            var fromStatus = change.Status.ToString();
            change.Status     = ChangeStatus.PendingAssessment;
            change.AssessedBy = _userManager.GetUserName(User);
            change.AssessedOn = DateTime.UtcNow;
            change.ExportFilePath = string.Empty;

            _context.Update(change);
            AddAuditLog(change.Id, "Submitted for Acceptance", fromStatus, ChangeStatus.PendingAssessment.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Change {change.ChangeId} submitted for acceptance by {change.AssessedBy}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Changes/AcceptChange
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptChange(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var change = await _context.Change.FindAsync(id);
            if (change == null)
                return NotFound();

            if (change.Status != ChangeStatus.PendingAssessment)
            {
                TempData["WorkflowError"] = "Change must be in Pending Acceptance status to accept.";
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

            var fromStatus = change.Status.ToString();
            change.Status     = ChangeStatus.Implementation;
            change.AcceptedBy = _userManager.GetUserName(User);
            change.AcceptedOn = DateTime.UtcNow;
            change.ExportFilePath = string.Empty;

            _context.Update(change);
            AddAuditLog(change.Id, "Accepted — Implementation Started", fromStatus, ChangeStatus.Implementation.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Change {change.ChangeId} accepted by {change.AcceptedBy}. Implementation can now begin.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Changes/SubmitForApproval
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.QualityUserRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var change = await _context.Change.FindAsync(id);
            if (change == null) return NotFound();

            if (change.Status != ChangeStatus.Implementation)
            {
                TempData["WorkflowError"] = "Change must be in Implementation status to submit for approval.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(change.Implementation))
            {
                TempData["WorkflowError"] = "Implementation details must be completed before submitting for approval.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Completion gate: when a detailed action plan is required, every applicable (non-N/A)
            // action item must have a completion date before the change can move to approval.
            var outstanding = await OutstandingActionItemsAsync(change);
            if (outstanding.Count > 0)
            {
                TempData["WorkflowError"] = $"The action plan has {outstanding.Count} outstanding item(s) — complete or mark them Not applicable before submitting for approval: {string.Join("; ", outstanding.Take(5))}{(outstanding.Count > 5 ? "…" : "")}";
                TempData["ActiveTab"] = "actionplan";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Evidence gate: each applicable item must record an evidence / reference before approval.
            var missingEvidence = (await ApplicableActionItemsAsync(change))
                .Where(i => string.IsNullOrWhiteSpace(i.EvidenceReference))
                .Select(i => Truncate(i.Action))
                .ToList();
            if (missingEvidence.Count > 0)
            {
                TempData["WorkflowError"] = $"Each action needs an evidence / reference before submitting for approval — {missingEvidence.Count} still missing: {string.Join("; ", missingEvidence.Take(5))}{(missingEvidence.Count > 5 ? "…" : "")}";
                TempData["ActiveTab"] = "actionplan";
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

            var fromStatus = change.Status.ToString();
            change.Status         = ChangeStatus.PendingApproval;
            change.ImplementedBy  = _userManager.GetUserName(User);
            change.ImplementedOn  = DateTime.UtcNow;
            change.ExportFilePath = string.Empty;

            _context.Update(change);
            AddAuditLog(change.Id, "Submitted for Approval", fromStatus, ChangeStatus.PendingApproval.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Change {change.ChangeId} submitted for approval by {change.ImplementedBy}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Changes/ApproveChange
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveChange(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var change = await _context.Change.FindAsync(id);
            if (change == null)
                return NotFound();

            if (change.Status != ChangeStatus.PendingApproval)
            {
                TempData["WorkflowError"] = "Change must be in Pending Approval status to approve.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Defensive re-check of the action-plan completion gate at closure.
            var outstandingAtApproval = await OutstandingActionItemsAsync(change);
            if (outstandingAtApproval.Count > 0)
            {
                TempData["WorkflowError"] = $"The action plan still has {outstandingAtApproval.Count} outstanding item(s); the change cannot be approved until all applicable actions are completed.";
                TempData["ActiveTab"] = "actionplan";
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

            var fromStatus = change.Status.ToString();
            change.Status     = ChangeStatus.Approved;
            change.ApprovedBy = _userManager.GetUserName(User);
            change.ApprovedOn = DateTime.UtcNow;
            change.ExportFilePath = string.Empty;
            var _chgSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            change.RetainUntil = change.ApprovedOn.Value.AddYears(_chgSets.ChangeRetentionYears);

            _context.Update(change);
            AddAuditLog(change.Id, "Approved", fromStatus, ChangeStatus.Approved.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Change {change.ChangeId} approved by {change.ApprovedBy}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Changes/ReturnChange
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnChange(int id, string email, string pwd, string? notes)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var change = await _context.Change.FindAsync(id);
            if (change == null) return NotFound();

            if (change.Status != ChangeStatus.PendingAssessment &&
                change.Status != ChangeStatus.PendingApproval   &&
                change.Status != ChangeStatus.Implementation)
            {
                TempData["WorkflowError"] = "Change cannot be returned from its current status.";
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

            var fromStatus = change.Status;
            ChangeStatus toStatus;
            string action;

            switch (fromStatus)
            {
                case ChangeStatus.PendingAssessment:
                    // Return to the previous working stage (Assessment) — not the initial stage —
                    // so the assessment can be reworked; consistent with Deviations/Complaints.
                    toStatus          = ChangeStatus.Assessment;
                    change.AssessedBy = null;
                    change.AssessedOn = null;
                    action            = "Returned to Assessment";
                    break;

                case ChangeStatus.PendingApproval:
                    toStatus             = ChangeStatus.Implementation;
                    change.ImplementedBy = null;
                    change.ImplementedOn = null;
                    action               = "Returned to Implementation";
                    break;

                case ChangeStatus.Implementation:
                    // Return to the previous working stage (Assessment), clearing the acceptance and
                    // assessment-submission stamps so the assessment and action plan unlock for rework —
                    // consistent with Deviations (Accepted → Evaluation), not a jump to the initial stage.
                    toStatus             = ChangeStatus.Assessment;
                    change.AssessedBy    = null;
                    change.AssessedOn    = null;
                    change.AcceptedBy    = null;
                    change.AcceptedOn    = null;
                    change.ImplementedBy = null;
                    change.ImplementedOn = null;
                    action               = "Returned to Assessment";
                    break;

                default:
                    TempData["WorkflowError"] = "Change cannot be returned from its current status.";
                    return RedirectToAction(nameof(Details), new { id });
            }

            change.Status = toStatus;
            change.ExportFilePath = string.Empty;
            _context.Update(change);
            AddAuditLog(change.Id, action, fromStatus.ToString(), toStatus.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Change {change.ChangeId} {action.ToLower()}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Changes/SaveActionPlan
        // Replaces a change's action-plan items from the submitted rows and logs a diff to the audit trail.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveActionPlan(int id,
            int[]? itemId, int[]? rowStatus, string[]? action, string[]? owner,
            string[]? targetDate, string[]? evidence, string[]? completedDate)
        {
            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole)
                  || User.IsInRole(Constants.QualityUserRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
                return Forbid();

            var change = await _context.Change.Include(c => c.ActionPlanItems).FirstOrDefaultAsync(c => c.Id == id);
            if (change == null) return NotFound();

            // Editable only while the change is open at Assessment or Implementation.
            if (change.Status != ChangeStatus.Assessment && change.Status != ChangeStatus.Implementation)
            {
                TempData["WorkflowError"] = "The action plan can only be edited while the change is in Assessment or Implementation.";
                TempData["ActiveTab"] = "actionplan";
                return RedirectToAction(nameof(Details), new { id });
            }

            itemId        ??= Array.Empty<int>();
            action        ??= Array.Empty<string>();
            owner         ??= Array.Empty<string>();
            rowStatus     ??= Array.Empty<int>();
            targetDate    ??= Array.Empty<string>();
            evidence      ??= Array.Empty<string>();
            completedDate ??= Array.Empty<string>();

            ActionItemStatus ParseStatus(int r) =>
                r < rowStatus.Length && Enum.IsDefined(typeof(ActionItemStatus), rowStatus[r])
                    ? (ActionItemStatus)rowStatus[r] : ActionItemStatus.NotStarted;
            DateTime? ParseDate(string[] arr, int r) =>
                r < arr.Length && DateTime.TryParse(arr[r], out var d) ? d : (DateTime?)null;
            // Cross-field validation: a completed date and a status of "Complete" must agree.
            string? CrossFieldError(string label, ActionItemStatus status, DateTime? comp)
            {
                if (comp.HasValue && status != ActionItemStatus.Complete)
                    return $"Action \"{Truncate(label)}\" has a completed date but its status is not \"Complete\". Set the status to Complete, or clear the completed date.";
                if (status == ActionItemStatus.Complete && !comp.HasValue)
                    return $"Action \"{Truncate(label)}\" is marked \"Complete\" but has no completed date. Add the completed date, or change the status.";
                return null;
            }

            var oldItems = change.ActionPlanItems?.OrderBy(i => i.DisplayOrder).ToList() ?? new List<ChangeActionPlanItem>();
            string diff;

            if (change.Status == ChangeStatus.Assessment)
            {
                // Plan-editing (Assessment): the whole plan is (re)defined — full replace from the rows.
                var newItems = new List<ChangeActionPlanItem>();
                for (int r = 0; r < action.Length; r++)
                {
                    var act = (action[r] ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(act)) continue;
                    var status = ParseStatus(r);
                    var comp   = ParseDate(completedDate, r);
                    var err = CrossFieldError(act, status, comp);
                    if (err != null) { TempData["WorkflowError"] = err; TempData["ActiveTab"] = "actionplan"; return RedirectToAction(nameof(Details), new { id }); }

                    newItems.Add(new ChangeActionPlanItem
                    {
                        ChangeId          = change.Id,
                        DisplayOrder      = newItems.Count,
                        Action            = act,
                        Owner             = string.IsNullOrWhiteSpace(owner.ElementAtOrDefault(r)) ? null : owner[r].Trim(),
                        Status            = status,
                        TargetDate        = ParseDate(targetDate, r),
                        EvidenceReference = string.IsNullOrWhiteSpace(evidence.ElementAtOrDefault(r)) ? null : evidence[r].Trim(),
                        CompletedDate     = comp
                    });
                }
                diff = BuildActionPlanDiff(oldItems, newItems);
                if (change.ActionPlanItems != null && change.ActionPlanItems.Any())
                    _context.ChangeActionPlanItem.RemoveRange(change.ActionPlanItems);
                await _context.ChangeActionPlanItem.AddRangeAsync(newItems);
            }
            else
            {
                // Execution (Implementation): the accepted plan is LOCKED — no add/delete, and Action/Owner/
                // Target date cannot change. Only Status / Evidence / Completed date are updated, matched to
                // existing items by Id. To change the plan itself, the change must be returned for rework.
                var oldSnapshot = oldItems.Select(i => new ChangeActionPlanItem
                {
                    Action = i.Action, Owner = i.Owner, TargetDate = i.TargetDate,
                    Status = i.Status, EvidenceReference = i.EvidenceReference,
                    CompletedDate = i.CompletedDate, DisplayOrder = i.DisplayOrder
                }).ToList();

                for (int r = 0; r < itemId.Length; r++)
                {
                    var item = oldItems.FirstOrDefault(i => i.Id == itemId[r]);
                    if (item == null) continue;
                    var status = ParseStatus(r);
                    var comp   = ParseDate(completedDate, r);
                    var err = CrossFieldError(item.Action, status, comp);
                    if (err != null) { TempData["WorkflowError"] = err; TempData["ActiveTab"] = "actionplan"; return RedirectToAction(nameof(Details), new { id }); }

                    item.Status            = status;
                    item.EvidenceReference = string.IsNullOrWhiteSpace(evidence.ElementAtOrDefault(r)) ? null : evidence[r].Trim();
                    item.CompletedDate     = comp;
                    // Action / Owner / TargetDate deliberately left unchanged (locked plan).
                }
                diff = BuildActionPlanDiff(oldSnapshot, oldItems);
            }

            change.ExportFilePath = string.Empty;
            AddAuditLog(change.Id, "Action Plan updated", null, null, diff);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Action plan saved.";
            TempData["ActiveTab"] = "actionplan";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Applicable (non-N/A) action items, ordered. Empty when no detailed plan is required.
        private async Task<List<ChangeActionPlanItem>> ApplicableActionItemsAsync(Change change)
        {
            if (!change.DetailedActionPlanRequired) return new List<ChangeActionPlanItem>();
            var items = change.ActionPlanItems?.ToList()
                        ?? await _context.ChangeActionPlanItem.Where(i => i.ChangeId == change.Id).ToListAsync();
            return items
                .Where(i => i.Status != ActionItemStatus.NotApplicable)
                .OrderBy(i => i.DisplayOrder)
                .ToList();
        }

        // Applicable action items not yet completed (no completed date). Used to gate approval.
        private async Task<List<string>> OutstandingActionItemsAsync(Change change)
            => (await ApplicableActionItemsAsync(change))
                .Where(i => i.CompletedDate == null)
                .Select(i => Truncate(i.Action))
                .ToList();

        private static string Truncate(string s, int n = 60)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        private static string BuildActionPlanDiff(List<ChangeActionPlanItem> oldItems, List<ChangeActionPlanItem> newItems)
        {
            string Key(ChangeActionPlanItem i) => i.Action.Trim();
            var oldByKey = oldItems.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
            var newByKey = newItems.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

            var parts = new List<string>();
            foreach (var k in newByKey.Keys.Where(k => !oldByKey.ContainsKey(k)))
                parts.Add($"added \"{Truncate(k)}\"");
            foreach (var k in oldByKey.Keys.Where(k => !newByKey.ContainsKey(k)))
                parts.Add($"removed \"{Truncate(k)}\"");

            int changed = 0;
            foreach (var k in newByKey.Keys.Where(oldByKey.ContainsKey))
            {
                var o = oldByKey[k]; var n = newByKey[k];
                if (o.Status != n.Status || o.Owner != n.Owner || o.TargetDate != n.TargetDate
                    || o.EvidenceReference != n.EvidenceReference || o.CompletedDate != n.CompletedDate)
                    changed++;
            }
            if (changed > 0) parts.Add($"{changed} item(s) updated");

            return parts.Count == 0
                ? $"Action plan saved ({newItems.Count} item(s); no changes)."
                : $"Action plan: {string.Join("; ", parts)} ({newItems.Count} item(s) total).";
        }

        // GET: Changes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            if (id == null || _context.Change == null)
                return NotFound();

            var change = await _context.Change
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Capa)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedCapas).ThenInclude(x => x.Capa)
                .Include(c => c.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (change == null)
                return NotFound();

            if (change.Status != ChangeStatus.Proposal && change.Status != ChangeStatus.Assessment)
                return Forbid();

            return View(change);
        }

        // POST: Changes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            if (_context.Change == null)
                return Problem("Entity set 'ApplicationDbContext.Change'  is null.");

            var change = await _context.Change
                .Include(c => c.Attachments)
                .Include(c => c.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (change == null)
                return NotFound();

            if (change.Status != ChangeStatus.Proposal && change.Status != ChangeStatus.Assessment)
                return Forbid();

            // Attachments (rows and files) are retained with the soft-deleted record.

            AddAuditLog(change.Id, "Deleted", change.Status.ToString(), null, change.Title);
            change.IsDeleted = true;
            change.DeletedBy = _userManager.GetUserName(User);
            change.DeletedOn = DateTime.UtcNow;
            _context.Change.Update(change);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private static string StatusLabel(ChangeStatus s) => s switch
        {
            ChangeStatus.PendingAssessment => "Pending Acceptance",
            ChangeStatus.PendingApproval   => "Pending Approval",
            _                              => s.ToString()
        };

        // GET: Changes/ExportRegister
        public async Task<IActionResult> ExportRegister()
        {
            var changes = await _context.Change
                .Where(x => !x.IsDeleted)
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Capa)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedCapas).ThenInclude(x => x.Capa)
                .OrderByDescending(c => c.ProposedOn)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Changes");

            // Header
            var headers = new[]
            {
                "Change ID", "Title", "Status", "Product", "Process", "Asset", "Material", "Supplier", "Linked CAPA",
                "Proposal", "Target Implementation Date",
                "Proposed By", "Proposed On",
                "Assessment", "Assessed By", "Assessed On",
                "Accepted By", "Accepted On",
                "Implementation", "Implemented By", "Implemented On",
                "Approved By", "Approved On"
            };

            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            headerRow.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var c in changes)
            {
                ws.Cell(row, 1).Value = c.ChangeId;
                ws.Cell(row, 2).Value = c.Title;
                ws.Cell(row, 3).Value = StatusLabel(c.Status);
                ws.Cell(row, 4).Value = JoinLinked(c.LinkedProducts, x => x.Product?.Name, c.Product?.Name);
                ws.Cell(row, 5).Value = JoinLinked(c.LinkedProcesses, x => x.Process?.Name, null);
                ws.Cell(row, 6).Value = JoinLinked(c.LinkedAssets, x => x.Asset?.Name, null);
                ws.Cell(row, 7).Value = JoinLinked(c.LinkedMaterials, x => x.Material?.Name, null);
                ws.Cell(row, 8).Value = JoinLinked(c.LinkedSuppliers, x => x.Supplier?.LegalName, c.Supplier?.LegalName);
                ws.Cell(row, 9).Value = JoinLinked(c.LinkedCapas, x => x.Capa != null ? $"{x.Capa.CapaId} - {x.Capa.Title}" : null, c.Capa != null ? $"{c.Capa.CapaId} - {c.Capa.Title}" : null);
                ws.Cell(row, 10).Value = c.Proposal;
                ws.Cell(row, 11).Value = c.TargetImplementationDate?.ToShortDateString() ?? "";
                ws.Cell(row, 12).Value = c.ProposedBy;
                ws.Cell(row, 13).Value = c.ProposedOn.ToSite().ToShortDateString();
                ws.Cell(row, 14).Value = c.Assessment ?? "";
                ws.Cell(row, 15).Value = c.AssessedBy ?? "";
                ws.Cell(row, 16).Value = c.AssessedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 17).Value = c.AcceptedBy ?? "";
                ws.Cell(row, 18).Value = c.AcceptedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 19).Value = c.Implementation ?? "";
                ws.Cell(row, 20).Value = c.ImplementedBy ?? "";
                ws.Cell(row, 21).Value = c.ImplementedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 22).Value = c.ApprovedBy ?? "";
                ws.Cell(row, 23).Value = c.ApprovedOn.ToSite()?.ToShortDateString() ?? "";

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");

                row++;
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            OpenQMS.Services.ExcelExportHelper.AddExportInfoSheet(wb, _userManager.GetUserName(User) ?? "Unknown", "Changes Register", changes.Count);

            // Register exports are recorded in the workbook's Export Info sheet (who / when / count).
            // No per-record audit entry: there is no parent record, and id 0 violates the audit-log FK.

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Changes-Register-{SiteTime.Today:yyyy-MM-dd}.xlsx");
        }

        public async Task<IActionResult> ExportChangeDetail(int id)
        {
            if (_context.Change == null)
                return NotFound();

            var change = await _context.Change
                .Where(x => x.Id == id)
                .Include(x => x.Product)
                .Include(x => x.Process)
                .Include(x => x.Asset)
                .Include(x => x.Material)
                .Include(x => x.Supplier)
                .Include(x => x.Capa)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.LinkedCapas).ThenInclude(x => x.Capa)
                .FirstOrDefaultAsync();

            if (change == null)
                return NotFound();

            var fields = new List<(string, string?)>
            {
                ("Change ID",       change.ChangeId),
                ("Title",           change.Title),
                ("Status",          StatusLabel(change.Status)),
                ("Product",         JoinLinked(change.LinkedProducts, x => x.Product?.Name, change.Product?.Name)),
                ("Process",         JoinLinked(change.LinkedProcesses, x => x.Process?.Name, null)),
                ("Asset",           JoinLinked(change.LinkedAssets, x => x.Asset?.Name, null)),
                ("Material",        JoinLinked(change.LinkedMaterials, x => x.Material?.Name, null)),
                ("Supplier",        JoinLinked(change.LinkedSuppliers, x => x.Supplier?.LegalName, change.Supplier?.LegalName)),
                ("Linked CAPAs",    JoinLinked(change.LinkedCapas, x => x.Capa != null ? $"{x.Capa.CapaId} - {x.Capa.Title}" : null, change.Capa?.Title)),
                ("Proposal",        change.Proposal),
                ("Proposed By",     change.ProposedBy),
                ("Proposed On",     change.ProposedOn.ToSite().ToShortDateString()),
                ("Assessment",      change.Assessment),
                ("Assessed By",     change.AssessedBy),
                ("Assessed On",     change.AssessedOn.ToSite()?.ToShortDateString()),
                ("Accepted By",     change.AcceptedBy),
                ("Accepted On",     change.AcceptedOn.ToSite()?.ToShortDateString()),
                ("Implementation",  change.Implementation),
                ("Implemented By",  change.ImplementedBy),
                ("Implemented On",  change.ImplementedOn.ToSite()?.ToShortDateString()),
                ("Approved By",     change.ApprovedBy),
                ("Approved On",     change.ApprovedOn.ToSite()?.ToShortDateString()),
            };

            AddAuditLog(change.Id, "Exported (PDF)", change.Status.ToString(), change.Status.ToString());
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Change Detail Report", change.ChangeId, StatusLabel(change.Status), fields,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Change-{change.ChangeId}.pdf");
        }

        public async Task<IActionResult> ExportDetailInCSV(int id)
        {
            if (_context.Change == null)
                return NotFound();

            var change = await _context.Change
                .Where(x => x.Id == id)
                .Include(x => x.Product).Include(x => x.Process)
                .Include(x => x.Asset).Include(x => x.Material)
                .Include(x => x.Supplier).Include(x => x.Capa)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.LinkedCapas).ThenInclude(x => x.Capa)
                .FirstOrDefaultAsync();

            if (change == null)
                return NotFound();

            AddAuditLog(change.Id, "Exported (CSV)", change.Status.ToString(), change.Status.ToString());
            await _context.SaveChangesAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Change Id,Title,Status,Product,Process,Asset,Material,Supplier,Linked CAPAs,Proposal,Proposed By,Proposed On,Assessment,Assessed By,Assessed On,Accepted By,Accepted On,Implementation,Implemented By,Implemented On,Approved By,Approved On");
            builder.AppendLine($"{CsvSafe(change.ChangeId)},{CsvSafe(change.Title)},{StatusLabel(change.Status)},{CsvSafe(JoinLinked(change.LinkedProducts, x => x.Product?.Name, change.Product?.Name))},{CsvSafe(JoinLinked(change.LinkedProcesses, x => x.Process?.Name, null))},{CsvSafe(JoinLinked(change.LinkedAssets, x => x.Asset?.Name, null))},{CsvSafe(JoinLinked(change.LinkedMaterials, x => x.Material?.Name, null))},{CsvSafe(JoinLinked(change.LinkedSuppliers, x => x.Supplier?.LegalName, change.Supplier?.LegalName))},{CsvSafe(JoinLinked(change.LinkedCapas, x => x.Capa != null ? $"{x.Capa.CapaId} - {x.Capa.Title}" : null, change.Capa?.Title))},{CsvSafe(change.Proposal)},{CsvSafe(change.ProposedBy)},{change.ProposedOn.ToSite().ToShortDateString()},{CsvSafe(change.Assessment)},{CsvSafe(change.AssessedBy)},{(change.AssessedOn.HasValue ? change.AssessedOn.Value.ToSite().ToShortDateString() : "")},{CsvSafe(change.AcceptedBy)},{(change.AcceptedOn.HasValue ? change.AcceptedOn.Value.ToSite().ToShortDateString() : "")},{CsvSafe(change.Implementation)},{CsvSafe(change.ImplementedBy)},{(change.ImplementedOn.HasValue ? change.ImplementedOn.Value.ToSite().ToShortDateString() : "")},{CsvSafe(change.ApprovedBy)},{(change.ApprovedOn.HasValue ? change.ApprovedOn.Value.ToSite().ToShortDateString() : "")}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Change.csv");
        }

        public ActionResult GetDocuments()
        {
            if (_context.Change == null)
                return NotFound();

            var changes = _context.Change.Where(x => !x.IsDeleted).ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            var orderedStatuses = new[]
            {
                ChangeStatus.Proposal,
                ChangeStatus.Assessment,
                ChangeStatus.PendingAssessment,
                ChangeStatus.Implementation,
                ChangeStatus.PendingApproval,
                ChangeStatus.Approved
            };

            if (changes.Count > 0)
            {
                foreach (var s in orderedStatuses)
                {
                    labels.Add(StatusLabel(s));
                    dataSet.Data.Add(changes.Count(x => x.Status == s));
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);
            return Json(new { dataSet = dataSets, labels });
        }

        private bool ChangeExists(int id)
        {
            return (_context.Change?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private static string JoinLinked<T>(ICollection<T>? junctions, Func<T, string?> getName, string? legacyFallback)
        {
            if (junctions?.Any() == true)
                return string.Join("; ", junctions.Select(getName).Where(n => !string.IsNullOrEmpty(n)));
            return legacyFallback ?? "";
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

        // GET: Changes/Attachments/5 (kept for backwards compatibility)
        public async Task<IActionResult> Attachments(int? id)
        {
            if (id == null)
                return NotFound();

            var change = await _context.Change
                .Include(c => c.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (change == null)
                return NotFound();

            return View(change);
        }

        // POST: Changes/UploadAttachment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int changeId, IFormFile file, string? description)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = changeId });
            }

            var change = await _context.Change.FindAsync(changeId);
            if (change == null)
                return NotFound();

            if (change.Status == Change.ChangeStatus.Approved)
            {
                TempData["Error"] = "Attachments cannot be added to an approved change record.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = changeId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, ZIP, JPG, PNG.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = changeId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds 25 MB limit.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = changeId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "changes", changeId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var safeName = Path.GetFileNameWithoutExtension(file.FileName)
                .Replace(" ", "_").Replace("..", "");
            var uniqueFileName = $"{timestamp}_{safeName}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var attachment = new ChangeAttachment
            {
                ChangeId = changeId,
                FileName = file.FileName,
                FilePath = $"/uploads/changes/{changeId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                Description = description
            };

            _context.ChangeAttachment.Add(attachment);
            AddAuditLog(changeId, "Attachment uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded successfully.";
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = changeId });
        }

        // GET: Changes/DownloadAttachment/5
        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null)
                return NotFound();

            var attachment = await _context.ChangeAttachment.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (attachment == null)
                return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ChangeId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.ChangeId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ChangeId });
            }

            AddAuditLog(attachment.ChangeId, (deleted ? "Deleted attachment downloaded" : "Attachment downloaded"), null, null, attachment.FileName);
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Change",
                attachment.ChangeId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.ChangeAttachment.FindAsync(id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ChangeId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.ChangeId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ChangeId });
            }

            AddAuditLog(attachment.ChangeId, "Attachment viewed", null, null, attachment.FileName);
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Change",
                attachment.ChangeId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream");
        }

        // POST: Changes/DeleteAttachment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int changeId, string? deletionReason)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var attachment = await _context.ChangeAttachment.FindAsync(id);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                changeId = attachment.ChangeId;

                var change = await _context.Change.FindAsync(changeId);
                if (change != null && change.Status == Change.ChangeStatus.Approved)
                {
                    TempData["Error"] = "Attachments cannot be removed from an approved change record.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = changeId });
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = changeId });
                }

                // Soft delete: the row and file are retained for traceability.
                attachment.IsDeleted      = true;
                attachment.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                AddAuditLog(changeId, "Attachment deleted", null, null, $"{attachment.FileName} — Reason: {attachment.DeletionReason}");
                await _context.SaveChangesAsync();

                TempData["Success"] = "Attachment deleted successfully.";
            }

            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = changeId });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AdminReopen(int id, string reason, string email, string pwd)
        {
            var change = await _context.Change.FindAsync(id);
            if (change == null) return NotFound();

            if (change.Status != ChangeStatus.Approved)
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

            var fromStatus = change.Status.ToString();
            change.Status = ChangeStatus.Implementation;
            AddAuditLog(change.Id, "Administrative Reopen", fromStatus, ChangeStatus.Implementation.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Record has been administratively reopened to Implementation stage.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
