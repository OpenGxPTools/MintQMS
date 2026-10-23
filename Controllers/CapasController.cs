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

using System.Data;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.ViewModels;
using OpenQMS.Services;
using static OpenQMS.Models.Capa;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class CapasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public CapasController
        (
           ApplicationDbContext context,
           UserManager<AppUser> userManager,
           SignInManager<AppUser> signInManager,
           IPasswordHasher<AppUser> passwordHasher
        )
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _passwordHasher = passwordHasher;
        }

        // GET: Capas
        public async Task<IActionResult> Index()
        {
            return _context.Capa != null ?
                View(await _context.Capa
                    .Where(c => !c.IsDeleted)
                    .Include(c => c.Product)
                    .Include(c => c.Deviation)
                    .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                    .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                    .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                    .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                    .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                    .Include(c => c.LinkedDeviations).ThenInclude(x => x.Deviation)
                    .Include(c => c.LinkedComplaints).ThenInclude(x => x.Complaint)
                    .Include(c => c.LinkedAudits).ThenInclude(x => x.Audit)
                    .ToListAsync()) :
                Problem("Entity set 'ApplicationDbContext.Capa'  is null.");
        }

        // GET: Capas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Capa == null)
            {
                return NotFound();
            }

            var capa = await _context.Capa
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Attachments)
                .Include(c => c.Deviation)
                .Include(c => c.Complaint)
                .Include(c => c.Audit)
                .Include(c => c.Changes)
                .Include(c => c.LinkedAdverseEvent)
                .Include(c => c.AuditLogs)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(c => c.LinkedComplaints).ThenInclude(x => x.Complaint)
                .Include(c => c.LinkedAudits).ThenInclude(x => x.Audit)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (capa == null)
            {
                return NotFound();
            }

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.CapaAttachment.DeletedOnly().Where(f => f.CapaId == id).ToListAsync();
            return View(capa);
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
            ViewData["Deviations"] = _context.Deviation
                .Where(x => !x.IsDeleted && (
                    x.Status == Deviation.DeviationStatus.Accepted ||
                    x.Status == Deviation.DeviationStatus.Resolution ||
                    x.Status == Deviation.DeviationStatus.Approved))
                .OrderByDescending(x => x.IdentifiedOn)
                .Take(100)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = $"{x.DeviationId} - {x.Title}" })
                .ToList();
            ViewData["Complaints"] = _context.Complaint
                .Where(x => !x.IsDeleted && (
                    x.Status == Complaint.ComplaintStatus.Accepted ||
                    x.Status == Complaint.ComplaintStatus.Resolution ||
                    x.Status == Complaint.ComplaintStatus.Closed))
                .OrderByDescending(x => x.ReceivedOn)
                .Take(100)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = $"{x.ComplaintId} - {x.Title}" })
                .ToList();
            ViewData["Audits"] = _context.Audit
                .Where(x => !x.IsDeleted && (
                    x.Status == Audit.AuditStatus.InProgress ||
                    x.Status == Audit.AuditStatus.ReportDraft ||
                    x.Status == Audit.AuditStatus.UnderReview ||
                    x.Status == Audit.AuditStatus.Approved))
                .OrderByDescending(x => x.ScheduledOn)
                .Take(100)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = $"{x.AuditId} - {x.Title}" })
                .ToList();
        }

        private async Task SyncCapaJunctions(int capaId,
            int[] productIds, int[] processIds, int[] assetIds,
            int[] materialIds, int[] supplierIds,
            int[] deviationIds, int[] complaintIds, int[] auditIds)
        {
            _context.CapaProduct.RemoveRange(
                await _context.CapaProduct.Where(x => x.CapaId == capaId).ToListAsync());
            _context.CapaProcess.RemoveRange(
                await _context.CapaProcess.Where(x => x.CapaId == capaId).ToListAsync());
            _context.CapaAsset.RemoveRange(
                await _context.CapaAsset.Where(x => x.CapaId == capaId).ToListAsync());
            _context.CapaMaterial.RemoveRange(
                await _context.CapaMaterial.Where(x => x.CapaId == capaId).ToListAsync());
            _context.CapaSupplier.RemoveRange(
                await _context.CapaSupplier.Where(x => x.CapaId == capaId).ToListAsync());
            _context.CapaDeviation.RemoveRange(
                await _context.CapaDeviation.Where(x => x.CapaId == capaId).ToListAsync());
            _context.CapaComplaint.RemoveRange(
                await _context.CapaComplaint.Where(x => x.CapaId == capaId).ToListAsync());
            _context.CapaAudit.RemoveRange(
                await _context.CapaAudit.Where(x => x.CapaId == capaId).ToListAsync());

            foreach (var id in productIds.Distinct())
                _context.CapaProduct.Add(new CapaProduct { CapaId = capaId, ProductId = id });
            foreach (var id in processIds.Distinct())
                _context.CapaProcess.Add(new CapaProcess { CapaId = capaId, ProcessId = id });
            foreach (var id in assetIds.Distinct())
                _context.CapaAsset.Add(new CapaAsset { CapaId = capaId, AssetId = id });
            foreach (var id in materialIds.Distinct())
                _context.CapaMaterial.Add(new CapaMaterial { CapaId = capaId, MaterialId = id });
            foreach (var id in supplierIds.Distinct())
                _context.CapaSupplier.Add(new CapaSupplier { CapaId = capaId, SupplierId = id });
            foreach (var id in deviationIds.Distinct())
                _context.CapaDeviation.Add(new CapaDeviation { CapaId = capaId, DeviationId = id });
            foreach (var id in complaintIds.Distinct())
                _context.CapaComplaint.Add(new CapaComplaint { CapaId = capaId, ComplaintId = id });
            foreach (var id in auditIds.Distinct())
                _context.CapaAudit.Add(new CapaAudit { CapaId = capaId, AuditId = id });
        }

        private static string JoinLinked<T>(ICollection<T>? junctions, Func<T, string?> getName, string? legacyFallback)
        {
            if (junctions?.Any() == true)
                return string.Join("; ", junctions.Select(getName).Where(n => !string.IsNullOrEmpty(n)));
            return legacyFallback ?? "";
        }

        // GET: Capas/Create
        public IActionResult Create(int? deviationId, int? complaintId, int? auditId, int? aeId, int? signalId)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole)
                && !User.IsInRole(Constants.AuditorRole)
                && !User.IsInRole(Constants.OperationsUserRole)
                && !User.IsInRole(Constants.CommercialUserRole))
                return Forbid();
            PopulateViewData();
            if (deviationId.HasValue) ViewBag.SelectedDeviationIds = new List<int> { deviationId.Value };
            if (complaintId.HasValue) ViewBag.SelectedComplaintIds = new List<int> { complaintId.Value };
            if (auditId.HasValue)     ViewBag.SelectedAuditIds     = new List<int> { auditId.Value };
            if (aeId.HasValue)
            {
                var ae = _context.AdverseEvent.Find(aeId.Value);
                if (ae != null)
                {
                    ViewBag.LinkedAEId    = ae.Id;
                    ViewBag.LinkedAETitle = ae.AENumber + (ae.IsSerious ? " [SERIOUS]" : "");
                    ViewBag.LinkedAEDesc  = ae.AEDescription;
                }
            }
            if (signalId.HasValue)
            {
                var signal = _context.PVSignalReview.Find(signalId.Value);
                if (signal != null && signal.LinkedCapaId == null)
                {
                    ViewBag.LinkedSignalId    = signal.Id;
                    ViewBag.LinkedSignalTitle = $"Signal — {signal.MedDRAPT}";
                }
            }
            return View();
        }

        // POST: Capas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Type,Title,CorrectiveAction,PreventiveAction,TargetCompletionDate,LinkedAdverseEventId")] Capa capa,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers,
            int[]? selectedDeviations, int[]? selectedComplaints, int[]? selectedAudits,
            int? signalId)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole)
                && !User.IsInRole(Constants.AuditorRole)
                && !User.IsInRole(Constants.OperationsUserRole)
                && !User.IsInRole(Constants.CommercialUserRole))
                return Forbid();

            if (capa.TargetCompletionDate.HasValue && capa.TargetCompletionDate.Value.Date < SiteTime.Today)
                ModelState.AddModelError(nameof(capa.TargetCompletionDate), "Target completion date must be today or in the future.");

            try
            {
                if (ModelState.IsValid)
                {
                    var lastId = _context.Capa
                        .Where(x => x.CapaId != null)
                        .AsEnumerable()
                        .Select(x => {
                            var parts = x.CapaId!.Split('-');
                            return parts.Length == 2 && int.TryParse(parts[1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    capa.CapaId = $"CPA-{(lastId + 1).ToString().PadLeft(4, '0')}";
                    capa.DeterminedBy = _userManager.GetUserName(User);
                    capa.DeterminedOn = DateTime.UtcNow;
                    // Default the target completion date from the configured CAPA resolution period when the
                    // originator leaves it blank (it stays user-editable) — activates DefaultCapaResolutionDays.
                    if (!capa.TargetCompletionDate.HasValue)
                    {
                        var settings = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
                        capa.TargetCompletionDate = capa.DeterminedOn.AddDays(settings.DefaultCapaResolutionDays);
                    }
                    capa.Status = Capa.CapaStatus.Determination;

                    // First save: required to get the auto-generated capa.Id
                    _context.Add(capa);
                    await _context.SaveChangesAsync();

                    // Sync junction tables
                    await SyncCapaJunctions(capa.Id,
                        selectedProducts   ?? Array.Empty<int>(),
                        selectedProcesses  ?? Array.Empty<int>(),
                        selectedAssets     ?? Array.Empty<int>(),
                        selectedMaterials  ?? Array.Empty<int>(),
                        selectedSuppliers  ?? Array.Empty<int>(),
                        selectedDeviations ?? Array.Empty<int>(),
                        selectedComplaints ?? Array.Empty<int>(),
                        selectedAudits     ?? Array.Empty<int>());

                    // Audit log + back-references
                    _context.CapaAuditLog.Add(new CapaAuditLog
                    {
                        CapaId = capa.Id,
                        Action = "CAPA Created",
                        ToStatus = capa.Status.ToString(),
                        PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                        PerformedOn = DateTime.UtcNow,
                        Notes = $"ID: {capa.CapaId}; Type: {capa.Type}; Title: {capa.Title}; Determined by: {capa.DeterminedBy}"
                    });

                    foreach (var devId in (selectedDeviations ?? Array.Empty<int>()))
                    {
                        var deviation = await _context.Deviation.FindAsync(devId);
                        if (deviation != null) { deviation.LinkedCapaId = capa.Id; _context.Update(deviation); }
                    }
                    foreach (var cmpId in (selectedComplaints ?? Array.Empty<int>()))
                    {
                        var complaint = await _context.Complaint.FindAsync(cmpId);
                        if (complaint != null) { complaint.LinkedCapaId = capa.Id; _context.Update(complaint); }
                    }
                    foreach (var audId in (selectedAudits ?? Array.Empty<int>()))
                    {
                        var audit = await _context.Audit.FindAsync(audId);
                        if (audit != null) { audit.LinkedCapaId = capa.Id; _context.Update(audit); }
                    }
                    if (capa.LinkedAdverseEventId.HasValue)
                    {
                        var ae = await _context.AdverseEvent.FindAsync(capa.LinkedAdverseEventId);
                        if (ae != null) { ae.LinkedCapaId = capa.Id; _context.Update(ae); }
                    }
                    if (signalId.HasValue)
                    {
                        var signal = await _context.PVSignalReview.FindAsync(signalId.Value);
                        if (signal != null && signal.LinkedCapaId == null)
                        {
                            signal.LinkedCapaId = capa.Id;
                            _context.Update(signal);
                        }
                    }

                    // Second save: commits junctions + audit log + back-references atomically
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Details), new { id = capa.Id });
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
            }

            PopulateViewData();
            ViewBag.SelectedProductIds   = selectedProducts?.ToList()   ?? new List<int>();
            ViewBag.SelectedProcessIds   = selectedProcesses?.ToList()  ?? new List<int>();
            ViewBag.SelectedAssetIds     = selectedAssets?.ToList()     ?? new List<int>();
            ViewBag.SelectedMaterialIds  = selectedMaterials?.ToList()  ?? new List<int>();
            ViewBag.SelectedSupplierIds  = selectedSuppliers?.ToList()  ?? new List<int>();
            ViewBag.SelectedDeviationIds = selectedDeviations?.ToList() ?? new List<int>();
            ViewBag.SelectedComplaintIds = selectedComplaints?.ToList() ?? new List<int>();
            ViewBag.SelectedAuditIds     = selectedAudits?.ToList()     ?? new List<int>();
            return View(capa);
        }

        // GET: Capas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (User.IsInRole(Constants.ReadOnlyRole))
                return Forbid();

            if (id == null || _context.Capa == null)
                return NotFound();

            var capa = await _context.Capa
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Deviation)
                .Include(c => c.Complaint)
                .Include(c => c.Audit)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(c => c.LinkedComplaints).ThenInclude(x => x.Complaint)
                .Include(c => c.LinkedAudits).ThenInclude(x => x.Audit)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (capa == null)
                return NotFound();

            if (capa.Status == CapaStatus.Approved ||
                capa.Status == CapaStatus.PendingAssessment ||
                capa.Status == CapaStatus.PendingApproval)
                return Forbid();

            PopulateViewData();

            ViewBag.SelectedProductIds   = capa.LinkedProducts?.Any() == true
                ? capa.LinkedProducts.Select(x => x.ProductId).ToList()
                : (capa.ProductId.HasValue ? new List<int> { capa.ProductId.Value } : new List<int>());
            ViewBag.SelectedProcessIds   = capa.LinkedProcesses?.Any() == true
                ? capa.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (capa.ProcessId.HasValue ? new List<int> { capa.ProcessId.Value } : new List<int>());
            ViewBag.SelectedAssetIds     = capa.LinkedAssets?.Any() == true
                ? capa.LinkedAssets.Select(x => x.AssetId).ToList()
                : (capa.AssetId.HasValue ? new List<int> { capa.AssetId.Value } : new List<int>());
            ViewBag.SelectedMaterialIds  = capa.LinkedMaterials?.Any() == true
                ? capa.LinkedMaterials.Select(x => x.MaterialId).ToList()
                : (capa.MaterialId.HasValue ? new List<int> { capa.MaterialId.Value } : new List<int>());
            ViewBag.SelectedSupplierIds  = capa.LinkedSuppliers?.Any() == true
                ? capa.LinkedSuppliers.Select(x => x.SupplierId).ToList()
                : (capa.SupplierId.HasValue ? new List<int> { capa.SupplierId.Value } : new List<int>());
            ViewBag.SelectedDeviationIds = capa.LinkedDeviations?.Any() == true
                ? capa.LinkedDeviations.Select(x => x.DeviationId).ToList()
                : (capa.DeviationId.HasValue ? new List<int> { capa.DeviationId.Value } : new List<int>());
            ViewBag.SelectedComplaintIds = capa.LinkedComplaints?.Any() == true
                ? capa.LinkedComplaints.Select(x => x.ComplaintId).ToList()
                : (capa.ComplaintId.HasValue ? new List<int> { capa.ComplaintId.Value } : new List<int>());
            ViewBag.SelectedAuditIds     = capa.LinkedAudits?.Any() == true
                ? capa.LinkedAudits.Select(x => x.AuditId).ToList()
                : (capa.AuditId.HasValue ? new List<int> { capa.AuditId.Value } : new List<int>());

            return View(capa);
        }

        // POST: Capas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormCollection form,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers,
            int[]? selectedDeviations, int[]? selectedComplaints, int[]? selectedAudits)
        {
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole)
                && !User.IsInRole(Constants.AuditorRole))
                return Forbid();

            var snap = await _context.Capa.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            var capa = await _context.Capa.FirstOrDefaultAsync(c => c.Id == id);
            if (capa == null || snap == null) return NotFound();

            if (capa.Status == CapaStatus.Approved ||
                capa.Status == CapaStatus.PendingAssessment ||
                capa.Status == CapaStatus.PendingApproval)
                return Forbid();

            var title            = form["Title"].FirstOrDefault() ?? capa.Title;
            var capaType         = Enum.TryParse<Capa.CapaType>(form["Type"].FirstOrDefault(), out var ct) ? ct : capa.Type;
            var correctiveAction = form["CorrectiveAction"].FirstOrDefault() ?? capa.CorrectiveAction;
            var preventiveAction = form["PreventiveAction"].FirstOrDefault() ?? capa.PreventiveAction;
            var assessment       = form["Assessment"].FirstOrDefault();
            var implementation   = form["Implementation"].FirstOrDefault();
            var targetDateStr    = form["TargetCompletionDate"].FirstOrDefault();
            DateTime? targetDate = DateTime.TryParse(targetDateStr, out var td) ? td : (DateTime?)null;
            int? linkedAEId      = int.TryParse(form["LinkedAdverseEventId"], out var ae) ? ae : (int?)null;

            if (string.IsNullOrWhiteSpace(title))
                ModelState.AddModelError("Title", "Title is required.");
            if (string.IsNullOrWhiteSpace(correctiveAction))
                ModelState.AddModelError("CorrectiveAction", "Corrective Action is required.");
            if (string.IsNullOrWhiteSpace(preventiveAction))
                ModelState.AddModelError("PreventiveAction", "Preventive Action is required.");
            if (targetDate.HasValue && targetDate.Value.Date < SiteTime.Today && targetDate != capa.TargetCompletionDate)
                ModelState.AddModelError("TargetCompletionDate", "Target completion date must be today or in the future.");

            if (!ModelState.IsValid)
            {
                PopulateViewData();
                ViewBag.SelectedProductIds   = selectedProducts?.ToList()   ?? new List<int>();
                ViewBag.SelectedProcessIds   = selectedProcesses?.ToList()  ?? new List<int>();
                ViewBag.SelectedAssetIds     = selectedAssets?.ToList()     ?? new List<int>();
                ViewBag.SelectedMaterialIds  = selectedMaterials?.ToList()  ?? new List<int>();
                ViewBag.SelectedSupplierIds  = selectedSuppliers?.ToList()  ?? new List<int>();
                ViewBag.SelectedDeviationIds = selectedDeviations?.ToList() ?? new List<int>();
                ViewBag.SelectedComplaintIds = selectedComplaints?.ToList() ?? new List<int>();
                ViewBag.SelectedAuditIds     = selectedAudits?.ToList()     ?? new List<int>();
                return View(capa);
            }

            var isDetermination  = capa.Status == CapaStatus.Determination;
            var isAssessment     = capa.Status == CapaStatus.Assessment;
            var isImplementation = capa.Status == CapaStatus.Implementation;

            // Capture affected-item / linked-record associations before sync (for the audit diff)
            var oldCaProductIds   = await _context.CapaProduct.Where(x => x.CapaId == capa.Id).Select(x => x.ProductId).ToListAsync();
            var oldCaProcessIds   = await _context.CapaProcess.Where(x => x.CapaId == capa.Id).Select(x => x.ProcessId).ToListAsync();
            var oldCaAssetIds     = await _context.CapaAsset.Where(x => x.CapaId == capa.Id).Select(x => x.AssetId).ToListAsync();
            var oldCaMaterialIds  = await _context.CapaMaterial.Where(x => x.CapaId == capa.Id).Select(x => x.MaterialId).ToListAsync();
            var oldCaSupplierIds  = await _context.CapaSupplier.Where(x => x.CapaId == capa.Id).Select(x => x.SupplierId).ToListAsync();
            var oldCaDeviationIds = await _context.CapaDeviation.Where(x => x.CapaId == capa.Id).Select(x => x.DeviationId).ToListAsync();
            var oldCaComplaintIds = await _context.CapaComplaint.Where(x => x.CapaId == capa.Id).Select(x => x.ComplaintId).ToListAsync();
            var oldCaAuditIds     = await _context.CapaAudit.Where(x => x.CapaId == capa.Id).Select(x => x.AuditId).ToListAsync();

            if (isDetermination || isAssessment)
            {
                capa.Title                = title;
                capa.Type                 = capaType;
                capa.CorrectiveAction     = correctiveAction;
                capa.PreventiveAction     = preventiveAction;
                capa.TargetCompletionDate = targetDate;
                capa.LinkedAdverseEventId = linkedAEId;
                capa.Assessment           = string.IsNullOrWhiteSpace(assessment) ? null : assessment;

                // Clear old single FKs — migrated to junction tables
                capa.ProductId   = null;
                capa.ProcessId   = null;
                capa.AssetId     = null;
                capa.MaterialId  = null;
                capa.SupplierId  = null;
                capa.DeviationId = null;
                capa.ComplaintId = null;
                capa.AuditId     = null;

                await SyncCapaJunctions(capa.Id,
                    selectedProducts   ?? Array.Empty<int>(),
                    selectedProcesses  ?? Array.Empty<int>(),
                    selectedAssets     ?? Array.Empty<int>(),
                    selectedMaterials  ?? Array.Empty<int>(),
                    selectedSuppliers  ?? Array.Empty<int>(),
                    selectedDeviations ?? Array.Empty<int>(),
                    selectedComplaints ?? Array.Empty<int>(),
                    selectedAudits     ?? Array.Empty<int>());
            }

            if (isImplementation)
                capa.Implementation = string.IsNullOrWhiteSpace(implementation) ? null : implementation;

            capa.ExportFilePath = string.Empty;

            var diff = new List<string>();
            AuditDiffHelper.TrackEnum(diff, "CAPA Type",            snap.Type,               capa.Type);
            AuditDiffHelper.Track(diff,     "Title",               snap.Title,             capa.Title);
            AuditDiffHelper.Track(diff,     "Corrective Action",   snap.CorrectiveAction,  capa.CorrectiveAction);
            AuditDiffHelper.Track(diff,     "Preventive Action",   snap.PreventiveAction,  capa.PreventiveAction);
            AuditDiffHelper.Track(diff,     "Assessment",          snap.Assessment,         capa.Assessment);
            AuditDiffHelper.Track(diff,     "Implementation",      snap.Implementation,    capa.Implementation);
            AuditDiffHelper.TrackDate(diff, "Target Completion",   snap.TargetCompletionDate, capa.TargetCompletionDate);
            if (isDetermination || isAssessment)
            {
                AuditDiffHelper.TrackSet(diff, "Affected Products",  oldCaProductIds,  selectedProducts  ?? Array.Empty<int>(),
                    ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Processes", oldCaProcessIds,  selectedProcesses ?? Array.Empty<int>(),
                    ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Assets",    oldCaAssetIds,    selectedAssets    ?? Array.Empty<int>(),
                    ids => _context.Asset.Where(a => ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Materials", oldCaMaterialIds, selectedMaterials ?? Array.Empty<int>(),
                    ids => _context.Material.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name));
                AuditDiffHelper.TrackSet(diff, "Affected Suppliers", oldCaSupplierIds, selectedSuppliers ?? Array.Empty<int>(),
                    ids => _context.Supplier.Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id, s => s.LegalName));
                AuditDiffHelper.TrackSet(diff, "Linked Deviations",  oldCaDeviationIds, selectedDeviations ?? Array.Empty<int>(),
                    ids => _context.Deviation.Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id, d => d.DeviationId));
                AuditDiffHelper.TrackSet(diff, "Linked Complaints",  oldCaComplaintIds, selectedComplaints ?? Array.Empty<int>(),
                    ids => _context.Complaint.Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id, c => c.ComplaintId));
                AuditDiffHelper.TrackSet(diff, "Linked Audits",      oldCaAuditIds,     selectedAudits     ?? Array.Empty<int>(),
                    ids => _context.Audit.Where(a => ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.AuditId));
            }

            if (diff.Count > 0)
            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = capa.Id,
                Action      = "Edited",
                FromStatus  = null,
                ToStatus    = null,
                Notes       = AuditDiffHelper.Build(diff),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = capa.Id });
        }

        // GET: Capas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            if (id == null || _context.Capa == null)
            {
                return NotFound();
            }

            var capa = await _context.Capa
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Attachments)
                .Include(c => c.Deviation)
                .Include(c => c.Changes)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(c => c.LinkedComplaints).ThenInclude(x => x.Complaint)
                .Include(c => c.LinkedAudits).ThenInclude(x => x.Audit)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (capa == null)
            {
                return NotFound();
            }

            if (capa.Status == CapaStatus.Approved ||
                capa.Status == CapaStatus.Implementation || capa.Status == CapaStatus.PendingAssessment ||
                capa.Status == CapaStatus.PendingApproval)
            {
                return Forbid();
            }

            return View(capa);
        }

        // POST: Capas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            if (_context.Capa == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Capa'  is null.");
            }

            var capa = await _context.Capa
                           .Include(c => c.Product)
                           .Include(c => c.Deviation)
                           .Include(c => c.Changes)
                           .FirstOrDefaultAsync(m => m.Id == id);

            if (capa != null)
            {
                if (capa.Status == CapaStatus.Approved ||
                    capa.Status == CapaStatus.Implementation || capa.Status == CapaStatus.PendingAssessment ||
                    capa.Status == CapaStatus.PendingApproval)
                {
                    return Forbid();
                }

                // Clear back-references — junction rows + legacy single FKs
                var devIds = (capa.LinkedDeviations?.Select(x => x.DeviationId) ?? Enumerable.Empty<int>())
                    .Concat(capa.DeviationId.HasValue ? new[] { capa.DeviationId.Value } : Array.Empty<int>())
                    .Distinct();
                foreach (var devId in devIds)
                {
                    var dev = await _context.Deviation.FindAsync(devId);
                    if (dev != null) { dev.LinkedCapaId = null; _context.Update(dev); }
                }
                var cmpIds = (capa.LinkedComplaints?.Select(x => x.ComplaintId) ?? Enumerable.Empty<int>())
                    .Concat(capa.ComplaintId.HasValue ? new[] { capa.ComplaintId.Value } : Array.Empty<int>())
                    .Distinct();
                foreach (var cmpId in cmpIds)
                {
                    var comp = await _context.Complaint.FindAsync(cmpId);
                    if (comp != null) { comp.LinkedCapaId = null; _context.Update(comp); }
                }
                var audIds = (capa.LinkedAudits?.Select(x => x.AuditId) ?? Enumerable.Empty<int>())
                    .Concat(capa.AuditId.HasValue ? new[] { capa.AuditId.Value } : Array.Empty<int>())
                    .Distinct();
                foreach (var audId in audIds)
                {
                    var audit = await _context.Audit.FindAsync(audId);
                    if (audit != null) { audit.LinkedCapaId = null; _context.Update(audit); }
                }
                if (capa.LinkedAdverseEventId.HasValue)
                {
                    var ae = await _context.AdverseEvent.FindAsync(capa.LinkedAdverseEventId.Value);
                    if (ae != null) { ae.LinkedCapaId = null; _context.Update(ae); }
                }

                _context.CapaAuditLog.Add(new CapaAuditLog
                {
                    CapaId = capa.Id,
                    Action = "Deleted",
                    FromStatus = capa.Status.ToString(),
                    ToStatus = null,
                    PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                    PerformedOn = DateTime.UtcNow
                });
                capa.IsDeleted = true;
                capa.DeletedBy = _userManager.GetUserName(User);
                capa.DeletedOn = DateTime.UtcNow;
                _context.Capa.Update(capa);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Capas/AcceptCapa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptCapa(int id, string email, string password, string? notes)
        {
            var capa = await _context.Capa.FindAsync(id);
            if (capa == null) return NotFound();

            // 21 CFR Part 11 §11.100(a): signature must be from the currently logged-in user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrEmpty(password) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, password) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)) || capa.Status != CapaStatus.PendingAssessment)
            {
                TempData["WorkflowError"] = "Not authorised to accept this CAPA in its current status.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = capa.Status;
            capa.Status = CapaStatus.Implementation;
            capa.AcceptedBy = _userManager.GetUserName(User);
            capa.AcceptedOn = DateTime.UtcNow;
            capa.ExportFilePath = string.Empty;
            _context.Update(capa);

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId = capa.Id,
                Action = "CAPA Accepted — Implementation Started",
                FromStatus = fromStatus.ToString(),
                ToStatus = capa.Status.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes = notes
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "CAPA accepted. Implementation can now begin.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Capas/ApproveCapa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCapa(int id, string email, string password, string? notes)
        {
            var capa = await _context.Capa.FindAsync(id);
            if (capa == null) return NotFound();

            // 21 CFR Part 11 §11.100(a): signature must be from the currently logged-in user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrEmpty(password) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, password) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)) || capa.Status != CapaStatus.PendingApproval)
            {
                TempData["WorkflowError"] = "Not authorised to approve this CAPA in its current status.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = capa.Status;
            capa.Status = CapaStatus.Approved;
            capa.ApprovedBy = _userManager.GetUserName(User);
            capa.ApprovedOn = DateTime.UtcNow;
            capa.ExportFilePath = string.Empty;
            var _capaSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            capa.RetainUntil = capa.ApprovedOn.Value.AddYears(_capaSets.CapaRetentionYears);
            _context.Update(capa);

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId = capa.Id,
                Action = "CAPA Approved",
                FromStatus = fromStatus.ToString(),
                ToStatus = capa.Status.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes = notes
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "CAPA approved successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Capas/SubmitForAcceptance — QU submits completed assessment for QM review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForAcceptance(int id, string email, string password, string? notes)
        {
            var capa = await _context.Capa.FindAsync(id);
            if (capa == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(password) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, password) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole)
                && !User.IsInRole(Constants.AuditorRole))
            {
                TempData["WorkflowError"] = "Not authorised to submit this CAPA for acceptance.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (capa.Status != CapaStatus.Determination && capa.Status != CapaStatus.Assessment)
            {
                TempData["WorkflowError"] = "CAPA cannot be submitted for acceptance from its current status.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(capa.CorrectiveAction) || string.IsNullOrWhiteSpace(capa.PreventiveAction))
            {
                TempData["WorkflowError"] = "Corrective Action and Preventive Action must be completed before submitting for acceptance.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(capa.Assessment))
            {
                TempData["WorkflowError"] = "Assessment must be completed before submitting for acceptance.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = capa.Status;
            capa.Status      = CapaStatus.PendingAssessment;
            capa.AssessedBy  = _userManager.GetUserName(User);
            capa.AssessedOn  = DateTime.UtcNow;
            capa.ExportFilePath = string.Empty;
            _context.Update(capa);

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = capa.Id,
                Action      = "Submitted for Acceptance",
                FromStatus  = fromStatus.ToString(),
                ToStatus    = capa.Status.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes       = notes
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "CAPA submitted for acceptance.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Capas/SubmitForApproval — QU submits completed implementation for QM approval
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(int id, string email, string password, string? notes)
        {
            var capa = await _context.Capa.FindAsync(id);
            if (capa == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(password) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, password) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (!User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole)
                && !User.IsInRole(Constants.AuditorRole))
            {
                TempData["WorkflowError"] = "Not authorised to submit this CAPA for approval.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (capa.Status != CapaStatus.Implementation)
            {
                TempData["WorkflowError"] = "CAPA must be in Implementation status to submit for approval.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(capa.Implementation))
            {
                TempData["WorkflowError"] = "Implementation details must be completed before submitting for approval.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = capa.Status;
            capa.Status        = CapaStatus.PendingApproval;
            capa.ImplementedBy = _userManager.GetUserName(User);
            capa.ImplementedOn = DateTime.UtcNow;
            capa.ExportFilePath = string.Empty;
            _context.Update(capa);

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = capa.Id,
                Action      = "Submitted for Approval",
                FromStatus  = fromStatus.ToString(),
                ToStatus    = capa.Status.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes       = notes
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "CAPA submitted for approval.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Capas/ReturnCapa — QM returns CAPA for rework, clears relevant By/On stamps
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnCapa(int id, string email, string password, string? notes)
        {
            var capa = await _context.Capa.FindAsync(id);
            if (capa == null) return NotFound();

            if (!(User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AuthorisedPersonRole)))
            {
                TempData["WorkflowError"] = "Only Quality Managers, Administrators and Authorised Persons can return a CAPA.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(password) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, password) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                TempData["ActiveTab"] = "workflow";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = capa.Status;
            string action;

            switch (capa.Status)
            {
                case CapaStatus.PendingAssessment:
                    capa.Status     = CapaStatus.Determination;
                    capa.AssessedBy = null;
                    capa.AssessedOn = null;
                    action = "Returned to Determination";
                    break;

                case CapaStatus.PendingApproval:
                    capa.Status        = CapaStatus.Implementation;
                    capa.ImplementedBy = null;
                    capa.ImplementedOn = null;
                    action = "Returned to Implementation";
                    break;

                case CapaStatus.Implementation:
                    capa.Status      = CapaStatus.Determination;
                    capa.AcceptedBy  = null;
                    capa.AcceptedOn  = null;
                    capa.AssessedBy  = null;
                    capa.AssessedOn  = null;
                    action = "Returned to Determination";
                    break;

                default:
                    TempData["WorkflowError"] = "CAPA cannot be returned from its current status.";
                    TempData["ActiveTab"] = "workflow";
                    return RedirectToAction(nameof(Details), new { id });
            }

            capa.ExportFilePath = string.Empty;
            _context.Update(capa);

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = capa.Id,
                Action      = action,
                FromStatus  = fromStatus.ToString(),
                ToStatus    = capa.Status.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes       = notes
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"CAPA returned for rework ({action}).";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> ExportCAPADetail(int id)
        {
            if (_context.Capa == null)
            {
                return NotFound();
            }

            var capa = await _context.Capa
                .Where(x => x.Id == id)
                .Include(x => x.Product)
                .Include(x => x.Process)
                .Include(x => x.Asset)
                .Include(x => x.Material)
                .Include(x => x.Supplier)
                .Include(x => x.Deviation)
                .Include(x => x.Changes)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(x => x.LinkedComplaints).ThenInclude(x => x.Complaint)
                .Include(x => x.LinkedAudits).ThenInclude(x => x.Audit)
                .FirstOrDefaultAsync();

            if (capa == null)
            {
                return NotFound();
            }

            var changeList = capa.Changes?.ToList() ?? new List<Change>();

            var fields = new List<(string, string?)>
            {
                ("CAPA ID",              capa.CapaId),
                ("Title",                capa.Title),
                ("Status",               capa.Status.ToString()),
                ("Products",             JoinLinked(capa.LinkedProducts,  x => x.Product?.Name,       capa.Product?.Name)),
                ("Processes",            JoinLinked(capa.LinkedProcesses, x => x.Process?.Name,       capa.Process?.Name)),
                ("Assets",               JoinLinked(capa.LinkedAssets,    x => x.Asset?.Name,         capa.Asset?.Name)),
                ("Materials",            JoinLinked(capa.LinkedMaterials, x => x.Material?.Name,      capa.Material?.Name)),
                ("Suppliers",            JoinLinked(capa.LinkedSuppliers, x => x.Supplier?.LegalName, capa.Supplier?.LegalName)),
                ("Linked Deviations",    JoinLinked(capa.LinkedDeviations, x => x.Deviation?.Title,   capa.Deviation?.Title)),
                ("Linked Complaints",    JoinLinked(capa.LinkedComplaints, x => x.Complaint?.Title,   capa.Complaint?.Title)),
                ("Linked Audits",        JoinLinked(capa.LinkedAudits,     x => x.Audit?.Title,       capa.Audit?.Title)),
                ("Corrective Action",    capa.CorrectiveAction),
                ("Preventive Action",    capa.PreventiveAction),
                ("Determined By",        capa.DeterminedBy),
                ("Determined On",        capa.DeterminedOn.ToSite().ToShortDateString()),
                ("Assessment",           capa.Assessment),
                ("Assessed By",          capa.AssessedBy),
                ("Assessed On",          capa.AssessedOn.ToSite()?.ToShortDateString()),
                ("Accepted By",          capa.AcceptedBy),
                ("Accepted On",          capa.AcceptedOn.ToSite()?.ToShortDateString()),
                ("Implementation",       capa.Implementation),
                ("Implemented By",       capa.ImplementedBy),
                ("Implemented On",       capa.ImplementedOn.ToSite()?.ToShortDateString()),
                ("Approved By",          capa.ApprovedBy),
                ("Approved On",          capa.ApprovedOn.ToSite()?.ToShortDateString()),
            };

            var tables = new List<(string, string[], IEnumerable<string[]>)>
            {
                ("Linked Changes",
                 new[] { "Change ID", "Title", "Status" },
                 changeList.Select(c => new[] { c.ChangeId, c.Title, c.Status.ToString() })),
            };

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = capa.Id,
                Action      = "Exported (PDF)",
                ToStatus    = capa.Status.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var pdfResult = PdfReportService.Generate("CAPA Detail Report", capa.CapaId, capa.Status.ToString(), fields, tables,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(pdfResult, "application/pdf", $"CAPA-{capa.CapaId}.pdf");
        }

        public async Task<IActionResult> ExportDetailInCSV(int id)
        {
            if (_context.Capa == null)
                return NotFound();

            var capa = await _context.Capa
                .Include(c => c.Product).Include(c => c.Process).Include(c => c.Asset)
                .Include(c => c.Material).Include(c => c.Supplier)
                .Include(c => c.Deviation).Include(c => c.Complaint).Include(c => c.Audit)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(c => c.LinkedComplaints).ThenInclude(x => x.Complaint)
                .Include(c => c.LinkedAudits).ThenInclude(x => x.Audit)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (capa == null)
                return NotFound();

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = capa.Id,
                Action      = "Exported (CSV)",
                ToStatus    = capa.Status.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var builder = new StringBuilder();
            builder.AppendLine("CAPA Id,Title,Status,Products,Processes,Assets,Materials,Suppliers,Deviations,Complaints,Audits,CorrectiveAction,PreventiveAction,TargetCompletionDate,Determined By,Determined On,Assessment,Assessed By,Assessed On,Accepted By,Accepted On,Implementation,Implemented By,Implemented On,Approved By,Approved On");
            builder.AppendLine($"{CsvSafe(capa.CapaId)},{CsvSafe(capa.Title)},{capa.Status},{CsvSafe(JoinLinked(capa.LinkedProducts,x=>x.Product?.Name,capa.Product?.Name))},{CsvSafe(JoinLinked(capa.LinkedProcesses,x=>x.Process?.Name,capa.Process?.Name))},{CsvSafe(JoinLinked(capa.LinkedAssets,x=>x.Asset?.Name,capa.Asset?.Name))},{CsvSafe(JoinLinked(capa.LinkedMaterials,x=>x.Material?.Name,capa.Material?.Name))},{CsvSafe(JoinLinked(capa.LinkedSuppliers,x=>x.Supplier?.LegalName,capa.Supplier?.LegalName))},{CsvSafe(JoinLinked(capa.LinkedDeviations,x=>x.Deviation?.Title,capa.Deviation?.Title))},{CsvSafe(JoinLinked(capa.LinkedComplaints,x=>x.Complaint?.Title,capa.Complaint?.Title))},{CsvSafe(JoinLinked(capa.LinkedAudits,x=>x.Audit?.Title,capa.Audit?.Title))},{CsvSafe(capa.CorrectiveAction)},{CsvSafe(capa.PreventiveAction)},{(capa.TargetCompletionDate.HasValue ? capa.TargetCompletionDate.Value.ToShortDateString() : "")},{CsvSafe(capa.DeterminedBy)},{capa.DeterminedOn.ToSite().ToShortDateString()},{CsvSafe(capa.Assessment)},{CsvSafe(capa.AssessedBy)},{(capa.AssessedOn.HasValue ? capa.AssessedOn.Value.ToSite().ToShortDateString() : "")},{CsvSafe(capa.AcceptedBy)},{(capa.AcceptedOn.HasValue ? capa.AcceptedOn.Value.ToSite().ToShortDateString() : "")},{CsvSafe(capa.Implementation)},{CsvSafe(capa.ImplementedBy)},{(capa.ImplementedOn.HasValue ? capa.ImplementedOn.Value.ToSite().ToShortDateString() : "")},{CsvSafe(capa.ApprovedBy)},{(capa.ApprovedOn.HasValue ? capa.ApprovedOn.Value.ToSite().ToShortDateString() : "")}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "CAPA.csv");
        }

        // GET: Capas/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var today = SiteTime.Today;
            var approaching = today.AddDays(14);

            var all = await _context.Capa
                .Include(c => c.Product)
                .Include(c => c.Deviation)
                .Include(c => c.Complaint)
                .Include(c => c.Audit)
                .ToListAsync();

            var orderedStatuses = new[]
            {
                CapaStatus.Determination, CapaStatus.Assessment, CapaStatus.PendingAssessment,
                CapaStatus.Implementation, CapaStatus.PendingApproval, CapaStatus.Approved
            };
            var statusCounts = orderedStatuses.ToDictionary(
                s => s == CapaStatus.PendingAssessment ? "Pending Assessment"
                   : s == CapaStatus.PendingApproval   ? "Pending Approval"
                   : s.ToString(),
                s => all.Count(c => c.Status == s));

            var open      = all.Where(c => c.Status != CapaStatus.Approved).ToList();
            var overdue   = open.Where(c => c.TargetCompletionDate.HasValue && c.TargetCompletionDate.Value < today).ToList();
            var approachingList = open.Where(c => c.TargetCompletionDate.HasValue
                                                   && c.TargetCompletionDate.Value >= today
                                                   && c.TargetCompletionDate.Value <= approaching
                                                   && !overdue.Contains(c)).ToList();

            ViewBag.TotalCapas       = all.Count;
            ViewBag.OpenCount        = open.Count;
            ViewBag.ApprovedCount    = all.Count(c => c.Status == CapaStatus.Approved);
            ViewBag.OverdueCount     = overdue.Count;
            ViewBag.ApproachingCount = approachingList.Count;
            ViewBag.StatusCounts     = statusCounts;

            ViewBag.OverdueCAPAs      = overdue.OrderBy(c => c.TargetCompletionDate).Take(10).ToList();
            ViewBag.ApproachingCAPAs  = approachingList.OrderBy(c => c.TargetCompletionDate).Take(10).ToList();
            ViewBag.PendingAcceptance = all.Where(c => c.Status == CapaStatus.PendingAssessment).OrderBy(c => c.DeterminedOn).Take(10).ToList();
            ViewBag.PendingApproval   = all.Where(c => c.Status == CapaStatus.PendingApproval).OrderBy(c => c.AcceptedOn).Take(10).ToList();

            // Recent audit log activity
            var recentLogs = await _context.CapaAuditLog
                .OrderByDescending(l => l.PerformedOn)
                .Take(12)
                .ToListAsync();

            var capaIds = recentLogs.Select(l => l.CapaId).Distinct().ToList();
            var recentCapaMap = await _context.Capa
                .Where(c => capaIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.CapaId);

            ViewBag.RecentActivity = recentLogs;
            ViewBag.RecentCapaMap  = recentCapaMap;

            return View();
        }

        // GET: Capas/ExportRegister
        public async Task<IActionResult> ExportRegister()
        {
            var capas = await _context.Capa
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Asset)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Deviation)
                .Include(c => c.Complaint)
                .Include(c => c.Audit)
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(c => c.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(c => c.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedDeviations).ThenInclude(x => x.Deviation)
                .Include(c => c.LinkedComplaints).ThenInclude(x => x.Complaint)
                .Include(c => c.LinkedAudits).ThenInclude(x => x.Audit)
                .OrderBy(c => c.CapaId)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("CAPA Register");

            var headers = new[]
            {
                "CAPA ID", "Title", "Status", "Target Completion",
                "Product", "Process", "Asset", "Material", "Supplier",
                "Linked Deviation", "Linked Complaint", "Linked Audit",
                "Corrective Action", "Preventive Action",
                "Determined By", "Determined On",
                "Assessment", "Assessed By", "Assessed On",
                "Accepted By", "Accepted On",
                "Implementation", "Implemented By", "Implemented On",
                "Approved By", "Approved On"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var c in capas)
            {
                ws.Cell(row, 1).Value  = c.CapaId;
                ws.Cell(row, 2).Value  = c.Title;
                ws.Cell(row, 3).Value  = c.Status.ToString();
                ws.Cell(row, 4).Value  = c.TargetCompletionDate?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(row, 5).Value  = JoinLinked(c.LinkedProducts,  x => x.Product?.Name,       c.Product?.Name)  ?? "";
                ws.Cell(row, 6).Value  = JoinLinked(c.LinkedProcesses, x => x.Process?.Name,       c.Process?.Name)  ?? "";
                ws.Cell(row, 7).Value  = JoinLinked(c.LinkedAssets,    x => x.Asset?.Name,         c.Asset?.Name)    ?? "";
                ws.Cell(row, 8).Value  = JoinLinked(c.LinkedMaterials, x => x.Material?.Name,      c.Material?.Name) ?? "";
                ws.Cell(row, 9).Value  = JoinLinked(c.LinkedSuppliers, x => x.Supplier?.LegalName, c.Supplier?.LegalName) ?? "";
                ws.Cell(row, 10).Value = JoinLinked(c.LinkedDeviations, x => x.Deviation != null ? $"{x.Deviation.DeviationId} – {x.Deviation.Title}" : "", c.Deviation != null ? $"{c.Deviation.DeviationId} – {c.Deviation.Title}" : "") ?? "";
                ws.Cell(row, 11).Value = JoinLinked(c.LinkedComplaints, x => x.Complaint != null ? $"{x.Complaint.ComplaintId} – {x.Complaint.Title}" : "", c.Complaint != null ? $"{c.Complaint.ComplaintId} – {c.Complaint.Title}" : "") ?? "";
                ws.Cell(row, 12).Value = JoinLinked(c.LinkedAudits,     x => x.Audit     != null ? $"{x.Audit.AuditId} – {x.Audit.Title}"             : "", c.Audit     != null ? $"{c.Audit.AuditId} – {c.Audit.Title}"             : "") ?? "";
                ws.Cell(row, 13).Value = c.CorrectiveAction;
                ws.Cell(row, 14).Value = c.PreventiveAction;
                ws.Cell(row, 15).Value = c.DeterminedBy;
                ws.Cell(row, 16).Value = c.DeterminedOn.ToSite().ToString("yyyy-MM-dd");
                ws.Cell(row, 17).Value = c.Assessment ?? "";
                ws.Cell(row, 18).Value = c.AssessedBy ?? "";
                ws.Cell(row, 19).Value = c.AssessedOn.ToSite()?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(row, 20).Value = c.AcceptedBy ?? "";
                ws.Cell(row, 21).Value = c.AcceptedOn.ToSite()?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(row, 22).Value = c.Implementation ?? "";
                ws.Cell(row, 23).Value = c.ImplementedBy ?? "";
                ws.Cell(row, 24).Value = c.ImplementedOn.ToSite()?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(row, 25).Value = c.ApprovedBy ?? "";
                ws.Cell(row, 26).Value = c.ApprovedOn.ToSite()?.ToString("yyyy-MM-dd") ?? "";

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");

                row++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            // Register exports are recorded in the workbook's Export Info sheet (who / when / count).
            // No per-record audit entry: there is no parent record, and id 0 violates the audit-log FK.

            OpenQMS.Services.ExcelExportHelper.AddExportInfoSheet(wb, _userManager.GetUserName(User) ?? "Unknown", "CAPA Register", capas.Count);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"CAPA-Register-{SiteTime.Today:yyyy-MM-dd}.xlsx");
        }

        public ActionResult GetDocuments()
        {
            if (_context.Capa == null)
            {
                return NotFound();
            }

            var capas = _context.Capa.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(CapaStatus)).Cast<CapaStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            if (capas != null && capas.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = capas.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        private bool CapaExists(int id)
        {
            return (_context.Capa?.Any(e => e.Id == id)).GetValueOrDefault();
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

        // POST: Capas/UploadAttachment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int capaId, IFormFile file, string? description)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = capaId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, Excel, ZIP, JPG, PNG.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = capaId });
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File size exceeds the 25 MB limit.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = capaId });
            }

            var capa = await _context.Capa.FindAsync(capaId);
            if (capa == null)
            {
                return NotFound();
            }

            if (capa.Status == Capa.CapaStatus.Approved)
            {
                TempData["Error"] = "Attachments cannot be added to an approved CAPA.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = capaId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "capas", capaId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var attachment = new CapaAttachment
            {
                CapaId = capaId,
                FileName = file.FileName,
                FilePath = $"/uploads/capas/{capaId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                Description = description
            };

            _context.CapaAttachment.Add(attachment);
            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = capaId,
                Action      = "Attachment uploaded",
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes       = file.FileName
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded successfully.";
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = capaId });
        }

        // GET: Capas/DownloadAttachment/5
        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null)
            {
                return NotFound();
            }

            var attachment = await _context.CapaAttachment.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (attachment == null)
            {
                return NotFound();
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.CapaId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                _context.CapaAuditLog.Add(new CapaAuditLog { CapaId = attachment.CapaId, Action = OpenQMS.Services.FileIntegrity.AuditAction, PerformedBy = _userManager.GetUserName(User) ?? "Unknown", PerformedOn = DateTime.UtcNow, Notes = OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName) });
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.CapaId });
            }

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = attachment.CapaId,
                Action      = (deleted ? "Deleted attachment downloaded" : "Attachment downloaded"),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes       = attachment.FileName
            });
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "CAPA",
                attachment.CapaId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.CapaAttachment.FindAsync(id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.CapaId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                _context.CapaAuditLog.Add(new CapaAuditLog { CapaId = attachment.CapaId, Action = OpenQMS.Services.FileIntegrity.AuditAction, PerformedBy = _userManager.GetUserName(User) ?? "Unknown", PerformedOn = DateTime.UtcNow, Notes = OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName) });
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.CapaId });
            }

            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId      = attachment.CapaId,
                Action      = "Attachment viewed",
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes       = attachment.FileName
            });
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "CAPA",
                attachment.CapaId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream");
        }

        // POST: Capas/DeleteAttachment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int capaId, string? deletionReason)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var attachment = await _context.CapaAttachment.FindAsync(id);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                capaId = attachment.CapaId;

                var capa = await _context.Capa.FindAsync(capaId);
                if (capa != null && capa.Status == Capa.CapaStatus.Approved)
                {
                    TempData["Error"] = "Attachments cannot be removed from an approved CAPA.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = capaId });
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = capaId });
                }

                // Soft delete: the row and file are retained for traceability.
                attachment.IsDeleted      = true;
                attachment.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                _context.CapaAuditLog.Add(new CapaAuditLog
                {
                    CapaId      = capaId,
                    Action      = "Attachment deleted",
                    PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                    PerformedOn = DateTime.UtcNow,
                    Notes       = $"{attachment.FileName} — Reason: {attachment.DeletionReason}"
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Attachment deleted successfully.";
            }

            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = capaId });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AdminReopen(int id, string reason, string email, string pwd)
        {
            var capa = await _context.Capa.FindAsync(id);
            if (capa == null) return NotFound();

            if (capa.Status != CapaStatus.Approved)
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

            var fromStatus = capa.Status.ToString();
            capa.Status = CapaStatus.Implementation;
            _context.CapaAuditLog.Add(new CapaAuditLog
            {
                CapaId = capa.Id,
                Action = "Administrative Reopen",
                FromStatus = fromStatus,
                ToStatus = CapaStatus.Implementation.ToString(),
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes = reason
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Record has been administratively reopened to Implementation stage.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
