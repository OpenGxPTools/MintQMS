/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2025  C-realize IT Services SRL (https://www.c-realize.com)
This program is offered under a commercial and under the AGPL license.
*/
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    // Class open to every role except OperationsUser & CommercialUser (they have no
    // access to the Risk module at all — registers, dashboard, details and exports).
    // Per-action ReadOnly guards below enforce the view-vs-write distinction; the
    // adverse/approval workflow actions add their own method-level role restrictions.
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class RisksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public RisksController(ApplicationDbContext context, UserManager<AppUser> userManager, IPasswordHasher<AppUser> passwordHasher)
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
                .Where(x => !x.IsDeleted && x.Status == OpenQMS.Models.Process.ProcessStatus.Approved)
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
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.LegalName + " (" + x.SupplierCode + ")" })
                .ToList();
        }

        private async Task SyncRiskJunctions(int riskId,
            int[] productIds, int[] processIds, int[] assetIds,
            int[] materialIds, int[] supplierIds)
        {
            _context.RiskProduct.RemoveRange(await _context.RiskProduct.Where(x => x.RiskId == riskId).ToListAsync());
            _context.RiskProcess.RemoveRange(await _context.RiskProcess.Where(x => x.RiskId == riskId).ToListAsync());
            _context.RiskAsset.RemoveRange(await _context.RiskAsset.Where(x => x.RiskId == riskId).ToListAsync());
            _context.RiskMaterial.RemoveRange(await _context.RiskMaterial.Where(x => x.RiskId == riskId).ToListAsync());
            _context.RiskSupplier.RemoveRange(await _context.RiskSupplier.Where(x => x.RiskId == riskId).ToListAsync());
            foreach (var id in productIds.Distinct())
                _context.RiskProduct.Add(new RiskProduct { RiskId = riskId, ProductId = id });
            foreach (var id in processIds.Distinct())
                _context.RiskProcess.Add(new RiskProcess { RiskId = riskId, ProcessId = id });
            foreach (var id in assetIds.Distinct())
                _context.RiskAsset.Add(new RiskAsset { RiskId = riskId, AssetId = id });
            foreach (var id in materialIds.Distinct())
                _context.RiskMaterial.Add(new RiskMaterial { RiskId = riskId, MaterialId = id });
            foreach (var id in supplierIds.Distinct())
                _context.RiskSupplier.Add(new RiskSupplier { RiskId = riskId, SupplierId = id });
        }

        private static string JoinLinked<T>(ICollection<T>? junctions, Func<T, string?> getName, string? legacyFallback)
        {
            if (junctions?.Any() == true)
                return string.Join("; ", junctions.Select(getName).Where(n => !string.IsNullOrEmpty(n)));
            return legacyFallback ?? "";
        }

        private string GenerateRiskId()
        {
            var next = _context.Risk
                .Select(x => x.RiskId)
                .AsEnumerable()
                .Select(rid =>
                {
                    if (!string.IsNullOrEmpty(rid))
                    {
                        var parts = rid.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var n)) return n;
                    }
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max() + 1;
            return $"RSK-{next.ToString().PadLeft(4, '0')}";
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.RiskAuditLog.Add(new RiskAuditLog
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

        // GET: Risks/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var risks = await _context.Risk
                .Where(r => !r.IsDeleted)
                .Include(r => r.AuditLogs)
                .ToListAsync();

            ViewBag.Total = risks.Count;
            ViewBag.StatusCounts = Enum.GetValues<Risk.RiskStatus>()
                .ToDictionary(s => s.ToString(), s => risks.Count(r => r.Status == s));
            ViewBag.OpenCount = risks.Count(r => r.Status != Risk.RiskStatus.Closed);
            ViewBag.ClosedCount = risks.Count(r => r.Status == Risk.RiskStatus.Closed);

            // RPN risk bands: High >= 80, Medium 40-79, Low < 40
            ViewBag.HighRPN = risks.Count(r => r.InitialRPN >= 80 && r.Status != Risk.RiskStatus.Closed);
            ViewBag.MediumRPN = risks.Count(r => r.InitialRPN >= 40 && r.InitialRPN < 80 && r.Status != Risk.RiskStatus.Closed);
            ViewBag.LowRPN = risks.Count(r => r.InitialRPN < 40 && r.Status != Risk.RiskStatus.Closed);

            ViewBag.CategoryCounts = Enum.GetValues<Risk.RiskCategory>()
                .ToDictionary(c => c.ToString(), c => risks.Count(r => r.Category == c));

            // Overdue: open risks with TargetCompletionDate in the past
            ViewBag.Overdue = risks
                .Where(r => r.Status != Risk.RiskStatus.Closed
                    && r.TargetCompletionDate.HasValue
                    && r.TargetCompletionDate.Value.Date < SiteTime.Today)
                .OrderBy(r => r.TargetCompletionDate)
                .Take(10).ToList();

            // Approaching: due within 14 days
            var in14 = SiteTime.Today.AddDays(14);
            ViewBag.Approaching = risks
                .Where(r => r.Status != Risk.RiskStatus.Closed
                    && r.TargetCompletionDate.HasValue
                    && r.TargetCompletionDate.Value.Date >= SiteTime.Today
                    && r.TargetCompletionDate.Value.Date <= in14)
                .OrderBy(r => r.TargetCompletionDate)
                .Take(10).ToList();

            // High RPN open risks
            ViewBag.HighRPNList = risks
                .Where(r => r.InitialRPN >= 80 && r.Status != Risk.RiskStatus.Closed)
                .OrderByDescending(r => r.InitialRPN)
                .Take(10).ToList();

            // Pending Assessment
            ViewBag.PendingAssessment = risks
                .Where(r => r.Status == Risk.RiskStatus.PendingAssessment)
                .OrderBy(r => r.IdentifiedOn)
                .Take(10).ToList();

            ViewBag.RecentActivity = risks
                .Where(r => r.AuditLogs != null)
                .SelectMany(r => r.AuditLogs!.Select(l => new { Log = l, Risk = r }))
                .OrderByDescending(x => x.Log.PerformedOn)
                .Take(12)
                .Select(x => new { x.Log, RiskId = x.Risk.RiskId, RecordId = x.Risk.Id, Title = x.Risk.Title })
                .ToList();

            return View();
        }

        // GET: Risks
        public async Task<IActionResult> Index()
        {
            var risks = await _context.Risk
                .Where(r => !r.IsDeleted)
                .Include(r => r.LinkedProducts).ThenInclude(x => x.Product)
                .Include(r => r.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(r => r.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(r => r.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(r => r.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(r => r.Product)
                .Include(r => r.Process)
                .Include(r => r.Asset)
                .Include(r => r.Material)
                .Include(r => r.Supplier)
                .OrderByDescending(r => r.IdentifiedOn)
                .ToListAsync();
            return View(risks);
        }

        // GET: Risks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Risk == null)
                return NotFound();

            var risk = await _context.Risk
                .Include(r => r.LinkedProducts).ThenInclude(x => x.Product)
                .Include(r => r.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(r => r.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(r => r.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(r => r.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(r => r.Product)
                .Include(r => r.Process)
                .Include(r => r.Asset)
                .Include(r => r.Material)
                .Include(r => r.Supplier)
                .Include(r => r.Attachments)
                .Include(r => r.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (risk == null)
                return NotFound();

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.RiskAttachment.DeletedOnly().Where(f => f.RiskId == id).ToListAsync();
            return View(risk);
        }

        // GET: Risks/Create
        public IActionResult Create()
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            PopulateViewData();
            return View();
        }

        // POST: Risks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Title,Description,Category,Severity,Probability,Detectability,RiskOwner,MitigationStrategy,TargetCompletionDate,MonitoringPlan,NextReviewDate")] Risk risk,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (risk.TargetCompletionDate.HasValue && risk.TargetCompletionDate.Value.Date < SiteTime.Today)
                ModelState.AddModelError(nameof(risk.TargetCompletionDate), "Target completion date must be today or in the future.");

            if (ModelState.IsValid)
            {
                risk.RiskId = GenerateRiskId();
                risk.InitialRPN = risk.Severity * risk.Probability * risk.Detectability;
                risk.IdentifiedBy = _userManager.GetUserName(User) ?? "Unknown";
                risk.IdentifiedOn = DateTime.UtcNow;
                risk.Status = Risk.RiskStatus.Identification;
                risk.ProductId = null; risk.ProcessId = null; risk.AssetId = null;
                risk.MaterialId = null; risk.SupplierId = null;

                _context.Add(risk);
                await _context.SaveChangesAsync();

                await SyncRiskJunctions(risk.Id,
                    selectedProducts ?? Array.Empty<int>(), selectedProcesses ?? Array.Empty<int>(),
                    selectedAssets ?? Array.Empty<int>(), selectedMaterials ?? Array.Empty<int>(),
                    selectedSuppliers ?? Array.Empty<int>());

                AddAuditLog(risk.Id, "Created", null, risk.Status.ToString(),
                    $"ID: {risk.RiskId}; Title: {risk.Title}; Category: {risk.Category}; Identified by: {risk.IdentifiedBy}");
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Risk {risk.RiskId} created.";
                return RedirectToAction(nameof(Details), new { id = risk.Id });
            }

            PopulateViewData();
            ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
            ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
            ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
            ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
            ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            return View(risk);
        }

        // GET: Risks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (id == null || _context.Risk == null)
                return NotFound();

            var risk = await _context.Risk
                .Where(r => r.Id == id)
                .Include(r => r.LinkedProducts).ThenInclude(x => x.Product)
                .Include(r => r.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(r => r.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(r => r.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(r => r.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (risk == null)
                return NotFound();

            if (risk.Status == Risk.RiskStatus.PendingAssessment ||
                risk.Status == Risk.RiskStatus.Review ||
                risk.Status == Risk.RiskStatus.PendingReview ||
                risk.Status == Risk.RiskStatus.Closed)
                return Forbid();

            ViewBag.SelectedProductIds  = risk.LinkedProducts?.Any()  == true
                ? risk.LinkedProducts.Select(x => x.ProductId).ToList()
                : (risk.ProductId.HasValue  ? new List<int> { risk.ProductId.Value }  : new List<int>());
            ViewBag.SelectedProcessIds  = risk.LinkedProcesses?.Any() == true
                ? risk.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (risk.ProcessId.HasValue  ? new List<int> { risk.ProcessId.Value }  : new List<int>());
            ViewBag.SelectedAssetIds    = risk.LinkedAssets?.Any()    == true
                ? risk.LinkedAssets.Select(x => x.AssetId).ToList()
                : (risk.AssetId.HasValue    ? new List<int> { risk.AssetId.Value }    : new List<int>());
            ViewBag.SelectedMaterialIds = risk.LinkedMaterials?.Any() == true
                ? risk.LinkedMaterials.Select(x => x.MaterialId).ToList()
                : (risk.MaterialId.HasValue ? new List<int> { risk.MaterialId.Value } : new List<int>());
            ViewBag.SelectedSupplierIds = risk.LinkedSuppliers?.Any() == true
                ? risk.LinkedSuppliers.Select(x => x.SupplierId).ToList()
                : (risk.SupplierId.HasValue ? new List<int> { risk.SupplierId.Value } : new List<int>());

            PopulateViewData();
            return View(risk);
        }

        // POST: Risks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            var snap = await _context.Risk.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            var risk = await _context.Risk.FindAsync(id);
            if (risk == null || snap == null) return NotFound();
            if (risk.Status == Risk.RiskStatus.PendingAssessment ||
                risk.Status == Risk.RiskStatus.Review ||
                risk.Status == Risk.RiskStatus.PendingReview ||
                risk.Status == Risk.RiskStatus.Closed)
                return Forbid();

            var oldRiskAcceptable = risk.RiskAcceptable;
            var oldStatus = risk.Status;
            var originalTargetDate = risk.TargetCompletionDate;

            var isIdentificationStage = risk.Status == Risk.RiskStatus.Identification
                                     || risk.Status == Risk.RiskStatus.Assessment;
            var isMitigationStage = risk.Status == Risk.RiskStatus.Mitigation;

            bool updated;
            if (isIdentificationStage)
            {
                updated = await TryUpdateModelAsync(risk, "",
                    r => r.Title, r => r.Description, r => r.Category,
                    r => r.Severity, r => r.Probability, r => r.Detectability,
                    r => r.RiskOwner, r => r.MitigationStrategy,
                    r => r.TargetCompletionDate, r => r.MonitoringPlan, r => r.NextReviewDate);
            }
            else // Mitigation
            {
                updated = await TryUpdateModelAsync(risk, "",
                    r => r.MitigationStrategy,
                    r => r.TargetCompletionDate, r => r.ActualCompletionDate,
                    r => r.ResidualSeverity, r => r.ResidualProbability, r => r.ResidualDetectability,
                    r => r.RiskAcceptable, r => r.Justification,
                    r => r.MonitoringPlan, r => r.NextReviewDate);
            }

            if (risk.RiskAcceptable.HasValue && string.IsNullOrWhiteSpace(risk.Justification))
                ModelState.AddModelError(nameof(risk.Justification),
                    "Justification is required when recording a risk acceptance decision.");

            if (risk.TargetCompletionDate.HasValue
                && risk.TargetCompletionDate != originalTargetDate
                && risk.TargetCompletionDate.Value.Date < SiteTime.Today)
                ModelState.AddModelError(nameof(risk.TargetCompletionDate), "Target completion date must be today or in the future.");

            if (updated && ModelState.IsValid)
            {
                try
                {
                    // Capture affected-item associations before sync (for the audit diff)
                    var oldRkProductIds  = await _context.RiskProduct.Where(x => x.RiskId == risk.Id).Select(x => x.ProductId).ToListAsync();
                    var oldRkProcessIds  = await _context.RiskProcess.Where(x => x.RiskId == risk.Id).Select(x => x.ProcessId).ToListAsync();
                    var oldRkAssetIds    = await _context.RiskAsset.Where(x => x.RiskId == risk.Id).Select(x => x.AssetId).ToListAsync();
                    var oldRkMaterialIds = await _context.RiskMaterial.Where(x => x.RiskId == risk.Id).Select(x => x.MaterialId).ToListAsync();
                    var oldRkSupplierIds = await _context.RiskSupplier.Where(x => x.RiskId == risk.Id).Select(x => x.SupplierId).ToListAsync();

                    if (isIdentificationStage)
                    {
                        risk.InitialRPN = risk.Severity * risk.Probability * risk.Detectability;
                        risk.ProductId = null; risk.ProcessId = null; risk.AssetId = null;
                        risk.MaterialId = null; risk.SupplierId = null;
                        await SyncRiskJunctions(risk.Id,
                            selectedProducts ?? Array.Empty<int>(), selectedProcesses ?? Array.Empty<int>(),
                            selectedAssets ?? Array.Empty<int>(), selectedMaterials ?? Array.Empty<int>(),
                            selectedSuppliers ?? Array.Empty<int>());
                    }
                    else // Mitigation
                    {
                        if (risk.ResidualSeverity.HasValue && risk.ResidualProbability.HasValue && risk.ResidualDetectability.HasValue)
                            risk.ResidualRPN = risk.ResidualSeverity.Value * risk.ResidualProbability.Value * risk.ResidualDetectability.Value;
                    }

                    var diff = new List<string>();
                    AuditDiffHelper.Track(diff,     "Title",               snap.Title,              risk.Title);
                    AuditDiffHelper.Track(diff,     "Description",         snap.Description,        risk.Description);
                    AuditDiffHelper.TrackEnum(diff, "Category",            snap.Category,            risk.Category);
                    AuditDiffHelper.Track<int>(diff, "Severity",           snap.Severity,            risk.Severity);
                    AuditDiffHelper.Track<int>(diff, "Probability",        snap.Probability,         risk.Probability);
                    AuditDiffHelper.Track<int>(diff, "Detectability",      snap.Detectability,       risk.Detectability);
                    AuditDiffHelper.Track(diff,     "Risk Owner",          snap.RiskOwner,           risk.RiskOwner);
                    AuditDiffHelper.Track(diff,     "Mitigation Strategy", snap.MitigationStrategy,  risk.MitigationStrategy);
                    AuditDiffHelper.TrackDate(diff, "Target Completion",   snap.TargetCompletionDate, risk.TargetCompletionDate);
                    AuditDiffHelper.Track<int>(diff, "Residual Severity",      snap.ResidualSeverity,      risk.ResidualSeverity);
                    AuditDiffHelper.Track<int>(diff, "Residual Probability",   snap.ResidualProbability,   risk.ResidualProbability);
                    AuditDiffHelper.Track<int>(diff, "Residual Detectability", snap.ResidualDetectability, risk.ResidualDetectability);
                    AuditDiffHelper.Track(diff,     "Justification",       snap.Justification,       risk.Justification);
                    AuditDiffHelper.Track(diff,     "Monitoring Plan",     snap.MonitoringPlan,      risk.MonitoringPlan);
                    AuditDiffHelper.TrackDate(diff, "Next Review Date",    snap.NextReviewDate,       risk.NextReviewDate);
                    if (isIdentificationStage)
                    {
                        AuditDiffHelper.TrackSet(diff, "Affected Products",  oldRkProductIds,  selectedProducts  ?? Array.Empty<int>(),
                            ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Processes", oldRkProcessIds,  selectedProcesses ?? Array.Empty<int>(),
                            ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Assets",    oldRkAssetIds,    selectedAssets    ?? Array.Empty<int>(),
                            ids => _context.Asset.Where(a => ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Materials", oldRkMaterialIds, selectedMaterials ?? Array.Empty<int>(),
                            ids => _context.Material.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Suppliers", oldRkSupplierIds, selectedSuppliers ?? Array.Empty<int>(),
                            ids => _context.Supplier.Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id, s => s.LegalName));
                    }

                    var acceptanceChanged = oldRiskAcceptable != risk.RiskAcceptable;
                    if (acceptanceChanged && risk.RiskAcceptable.HasValue)
                        AddAuditLog(risk.Id, risk.RiskAcceptable.Value ? "Risk Accepted" : "Risk Not Accepted",
                            oldStatus.ToString(), risk.Status.ToString(),
                            $"Justification: {risk.Justification}" + (diff.Count > 0 ? " | " + AuditDiffHelper.Build(diff) : ""));
                    else
                        if (diff.Count > 0) AddAuditLog(risk.Id, "Edited", null, null, AuditDiffHelper.Build(diff));

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RiskExists(risk.Id)) return NotFound();
                    else throw;
                }

                TempData["Success"] = "Risk updated.";
                return RedirectToAction(nameof(Details), new { id = risk.Id });
            }

            PopulateViewData();
            if (isIdentificationStage)
            {
                ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
                ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
                ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
                ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
                ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            }
            return View(risk);
        }

        // GET: Risks/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Risk == null)
                return NotFound();

            var risk = await _context.Risk
                .Include(r => r.LinkedProducts).ThenInclude(x => x.Product)
                .Include(r => r.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(r => r.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(r => r.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(r => r.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(r => r.Product).Include(r => r.Process)
                .Include(r => r.Asset).Include(r => r.Material).Include(r => r.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (risk == null)
                return NotFound();

            if (risk.Status != Risk.RiskStatus.Identification)
                return Forbid();

            return View(risk);
        }

        // POST: Risks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var risk = await _context.Risk
                .Include(r => r.Attachments)
                .Include(r => r.AuditLogs)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (risk != null)
            {
                if (risk.Status != Risk.RiskStatus.Identification)
                    return Forbid();

                // Attachments (rows and files) are retained with the soft-deleted record.

                AddAuditLog(risk.Id, "Deleted", risk.Status.ToString(), null, risk.Title);
                risk.IsDeleted = true;
                risk.DeletedBy = _userManager.GetUserName(User);
                risk.DeletedOn = DateTime.UtcNow;
                _context.Risk.Update(risk);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Risks/SubmitForAssessment — any authenticated user submits a risk for QM assessment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForAssessment(int id, string email, string pwd, string? notes)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Submission was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var risk = await _context.Risk.FindAsync(id);
            if (risk == null) return NotFound();
            if (risk.Status != Risk.RiskStatus.Identification)
            {
                TempData["WorkflowError"] = "Risk must be in Identification status to submit for assessment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = risk.Status.ToString();
            risk.AssessedBy  = _userManager.GetUserName(User);
            risk.AssessedOn  = DateTime.UtcNow;
            risk.Status      = Risk.RiskStatus.PendingAssessment;

            AddAuditLog(risk.Id, "Submitted for Assessment", from, risk.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Risk submitted for Quality Manager assessment.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Risks/AcceptAssessment — Admin/QM formally accepts the assessment; moves to Mitigation
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> AcceptAssessment(int id, string email, string pwd, string? assessmentNotes, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Assessment acceptance was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var risk = await _context.Risk.FindAsync(id);
            if (risk == null) return NotFound();
            if (risk.Status != Risk.RiskStatus.PendingAssessment)
            {
                TempData["WorkflowError"] = "Risk must be in Pending Assessment status to accept.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = risk.Status.ToString();
            risk.AcceptedBy      = _userManager.GetUserName(User);
            risk.AcceptedOn      = DateTime.UtcNow;
            risk.AssessmentNotes = string.IsNullOrWhiteSpace(assessmentNotes) ? null : assessmentNotes;
            risk.Status          = Risk.RiskStatus.Mitigation;

            AddAuditLog(risk.Id, "Assessment Accepted — Mitigation Started", from, risk.Status.ToString(),
                string.IsNullOrWhiteSpace(notes) ? assessmentNotes : notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Assessment accepted. Risk moved to Mitigation.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Risks/ReturnFromAssessment — Admin/QM returns the risk to Identification for revision
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> ReturnFromAssessment(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Return was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var risk = await _context.Risk.FindAsync(id);
            if (risk == null) return NotFound();
            if (risk.Status != Risk.RiskStatus.PendingAssessment)
            {
                TempData["WorkflowError"] = "Risk must be in Pending Assessment status to return for revision.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = risk.Status.ToString();
            risk.AssessedBy = null;
            risk.AssessedOn = null;
            risk.Status     = Risk.RiskStatus.Identification;

            AddAuditLog(risk.Id, "Returned to Identification", from, risk.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Risk returned to Identification for revision.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Risks/SubmitForReview — any authenticated user submits completed mitigation for QM review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(int id, string email, string pwd, string? notes)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Submission was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var risk = await _context.Risk.FindAsync(id);
            if (risk == null) return NotFound();
            if (risk.Status != Risk.RiskStatus.Mitigation && risk.Status != Risk.RiskStatus.Assessment)
            {
                TempData["WorkflowError"] = "Risk must be in Mitigation status to submit for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(risk.MitigationStrategy))
            {
                TempData["WorkflowError"] = "Mitigation Strategy must be completed before submitting for review.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (!risk.ResidualSeverity.HasValue || !risk.ResidualProbability.HasValue || !risk.ResidualDetectability.HasValue)
            {
                TempData["WorkflowError"] = "Residual Risk Assessment (Severity, Probability, Detectability) must be completed before submitting for review.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (!risk.RiskAcceptable.HasValue)
            {
                TempData["WorkflowError"] = "Risk Acceptance decision must be recorded before submitting for review.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(risk.Justification))
            {
                TempData["WorkflowError"] = "Justification for the risk acceptance decision must be completed before submitting for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = risk.Status.ToString();
            risk.ReviewedBy = _userManager.GetUserName(User);
            risk.ReviewedOn = DateTime.UtcNow;
            risk.Status     = Risk.RiskStatus.PendingReview;

            AddAuditLog(risk.Id, "Submitted for Review", from, risk.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Risk submitted for Quality Manager review and closure.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Risks/CloseRisk — Admin/QM reviews and formally closes the risk
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> CloseRisk(int id, string email, string pwd, string reviewConclusion, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Risk was not closed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reviewConclusion))
            {
                TempData["WorkflowError"] = "Review Conclusion is required to close the risk.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var risk = await _context.Risk.FindAsync(id);
            if (risk == null) return NotFound();
            if (risk.Status != Risk.RiskStatus.PendingReview && risk.Status != Risk.RiskStatus.Review)
            {
                TempData["WorkflowError"] = "Risk must be in Pending Review status to be closed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = risk.Status.ToString();
            risk.ReviewConclusion = reviewConclusion;
            risk.ClosedBy         = _userManager.GetUserName(User);
            risk.ClosedOn         = DateTime.UtcNow;
            risk.Status           = Risk.RiskStatus.Closed;
            var settings = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            risk.RetainUntil = risk.ClosedOn.Value.AddYears(settings.RiskRetentionYears);

            AddAuditLog(risk.Id, "Risk Closed", from, risk.Status.ToString(),
                string.IsNullOrWhiteSpace(notes) ? reviewConclusion : $"{reviewConclusion} | {notes}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Risk closed.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Risks/ReturnFromReview — Admin/QM returns the risk to Mitigation for further work
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> ReturnFromReview(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Return was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var risk = await _context.Risk.FindAsync(id);
            if (risk == null) return NotFound();
            if (risk.Status != Risk.RiskStatus.PendingReview && risk.Status != Risk.RiskStatus.Review)
            {
                TempData["WorkflowError"] = "Risk must be in Pending Review status to return to Mitigation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = risk.Status.ToString();
            risk.ReviewedBy = null;
            risk.ReviewedOn = null;
            risk.Status     = Risk.RiskStatus.Mitigation;

            AddAuditLog(risk.Id, "Returned to Mitigation", from, risk.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Risk returned to Mitigation for further work.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Risks/UploadAttachment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int riskId, IFormFile file, string? description)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = riskId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, Excel, ZIP, JPG, PNG.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = riskId });
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File size exceeds the 25 MB limit.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = riskId });
            }

            var risk = await _context.Risk.FindAsync(riskId);
            if (risk == null) return NotFound();
            if (risk.Status == Risk.RiskStatus.Closed) return Forbid();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "risks", riskId.ToString());
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

            _context.RiskAttachment.Add(new RiskAttachment
            {
                RiskId = riskId,
                FileName = file.FileName,
                FilePath = $"/uploads/risks/{riskId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                Description = description
            });

            AddAuditLog(riskId, "Attachment uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded.";
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = riskId });
        }

        // GET: Risks/DownloadAttachment/5
        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();
            var attachment = await _context.RiskAttachment.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.RiskId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.RiskId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.RiskId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Risk",
                attachment.RiskId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.RiskAttachment.FindAsync(id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.RiskId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.RiskId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.RiskId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Risk",
                attachment.RiskId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream");
        }

        // POST: Risks/DeleteAttachment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int riskId, string? deletionReason)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var attachment = await _context.RiskAttachment.FindAsync(id);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                riskId = attachment.RiskId;
                var parentRisk = await _context.Risk.FindAsync(riskId);
                if (parentRisk?.Status == Risk.RiskStatus.Closed) return Forbid();

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = riskId });
                }

                // Soft delete: the row and file are retained for traceability.
                attachment.IsDeleted      = true;
                attachment.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                AddAuditLog(riskId, "Attachment deleted", null, null, $"{attachment.FileName} — Reason: {attachment.DeletionReason}");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attachment deleted.";
            }

            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = riskId });
        }

        // GET: Risks/ExportRegister
        public async Task<IActionResult> ExportRegister()
        {
            var risks = await _context.Risk
                .Where(r => !r.IsDeleted)
                .Include(r => r.LinkedProducts).ThenInclude(x => x.Product)
                .Include(r => r.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(r => r.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(r => r.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(r => r.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(r => r.Product).Include(r => r.Process)
                .Include(r => r.Asset).Include(r => r.Material).Include(r => r.Supplier)
                .OrderByDescending(r => r.IdentifiedOn)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Risk Register");

            // Header
            var headers = new[] { "Risk ID", "Title", "Category", "Status", "Severity", "Probability", "Detectability",
                "Initial RPN", "Residual RPN", "Risk Acceptable", "Risk Owner", "Identified By", "Identified On",
                "Target Completion", "Product", "Process", "Asset", "Material", "Supplier" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            ws.SheetView.FreezeRows(1);

            for (int i = 0; i < risks.Count; i++)
            {
                var r = risks[i];
                var row = i + 2;
                var fill = i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#F2F2F2");
                ws.Cell(row, 1).Value = r.RiskId;
                ws.Cell(row, 2).Value = r.Title;
                ws.Cell(row, 3).Value = r.Category.ToString();
                ws.Cell(row, 4).Value = r.Status.ToString();
                ws.Cell(row, 5).Value = r.Severity;
                ws.Cell(row, 6).Value = r.Probability;
                ws.Cell(row, 7).Value = r.Detectability;
                ws.Cell(row, 8).Value = r.InitialRPN;
                ws.Cell(row, 9).Value = r.ResidualRPN?.ToString() ?? "";
                ws.Cell(row, 10).Value = r.RiskAcceptable.HasValue ? (r.RiskAcceptable.Value ? "Yes" : "No") : "";
                ws.Cell(row, 11).Value = r.RiskOwner ?? "";
                ws.Cell(row, 12).Value = r.IdentifiedBy;
                ws.Cell(row, 13).Value = r.IdentifiedOn.ToSite().ToShortDateString();
                ws.Cell(row, 14).Value = r.TargetCompletionDate?.ToShortDateString() ?? "";
                ws.Cell(row, 15).Value = JoinLinked(r.LinkedProducts,  x => x.Product?.Name,      r.Product?.Name);
                ws.Cell(row, 16).Value = JoinLinked(r.LinkedProcesses, x => x.Process?.Name,      r.Process?.Name);
                ws.Cell(row, 17).Value = JoinLinked(r.LinkedAssets,    x => x.Asset?.Name,        r.Asset?.Name);
                ws.Cell(row, 18).Value = JoinLinked(r.LinkedMaterials, x => x.Material?.Name,     r.Material?.Name);
                ws.Cell(row, 19).Value = JoinLinked(r.LinkedSuppliers, x => x.Supplier?.LegalName, r.Supplier?.LegalName);
                for (int c = 1; c <= headers.Length; c++)
                    ws.Cell(row, c).Style.Fill.BackgroundColor = fill;
            }

            ws.Columns().AdjustToContents();

            OpenQMS.Services.ExcelExportHelper.AddExportInfoSheet(wb, _userManager.GetUserName(User) ?? "Unknown", "Risk Register", risks.Count);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            // Register exports are recorded in the workbook's Export Info sheet (who / when / count).
            // No per-record audit entry: there is no parent record, and id 0 violates the audit-log FK.

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"RiskRegister_{SiteTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> ExportRiskDetail(int id)
        {
            if (_context.Risk == null) return NotFound();
            var risk = await _context.Risk
                .Where(x => x.Id == id)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.Product).Include(x => x.Process)
                .Include(x => x.Asset).Include(x => x.Material).Include(x => x.Supplier)
                .FirstOrDefaultAsync();
            if (risk == null) return NotFound();

            string? L(string s) => s.Length > 0 ? s : null;
            var fields = new List<(string, string?)>
            {
                ("Risk ID",              risk.RiskId),
                ("Title",                risk.Title),
                ("Status",               risk.Status.ToString()),
                ("Category",             risk.Category.ToString()),
                ("Description",          risk.Description),
                ("Risk Owner",           risk.RiskOwner),
                ("Product",  L(JoinLinked(risk.LinkedProducts,  x => x.Product?.Name,       risk.Product?.Name))),
                ("Process",  L(JoinLinked(risk.LinkedProcesses, x => x.Process?.Name,       risk.Process?.Name))),
                ("Asset",    L(JoinLinked(risk.LinkedAssets,    x => x.Asset?.Name,         risk.Asset?.Name))),
                ("Material", L(JoinLinked(risk.LinkedMaterials, x => x.Material?.Name,      risk.Material?.Name))),
                ("Supplier", L(JoinLinked(risk.LinkedSuppliers, x => x.Supplier?.LegalName, risk.Supplier?.LegalName))),
                ("Identified By",        risk.IdentifiedBy),
                ("Identified On",        risk.IdentifiedOn.ToSite().ToShortDateString()),
                ("Severity",             risk.Severity.ToString()),
                ("Probability",          risk.Probability.ToString()),
                ("Detectability",        risk.Detectability.ToString()),
                ("Initial RPN",          risk.InitialRPN.ToString()),
                ("Mitigation Strategy",  risk.MitigationStrategy),
                ("Target Completion",    risk.TargetCompletionDate?.ToShortDateString()),
                ("Actual Completion",    risk.ActualCompletionDate?.ToShortDateString()),
                ("Mitigated By",         risk.MitigatedBy),
                ("Mitigated On",         risk.MitigatedOn?.ToShortDateString()),
                ("Residual RPN",         risk.ResidualRPN?.ToString()),
                ("Risk Acceptable",      risk.RiskAcceptable.HasValue ? (risk.RiskAcceptable.Value ? "Yes" : "No") : null),
                ("Justification",        risk.Justification),
                ("Reviewed By",          risk.ReviewedBy),
                ("Reviewed On",          risk.ReviewedOn.ToSite()?.ToShortDateString()),
                ("Closed By",            risk.ClosedBy),
                ("Closed On",            risk.ClosedOn.ToSite()?.ToShortDateString()),
            };

            AddAuditLog(risk.Id, "Exported (PDF)", risk.Status.ToString(), risk.Status.ToString());
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Risk Detail Report", risk.RiskId, risk.Status.ToString(), fields,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Risk-{risk.RiskId}.pdf");
        }

        public async Task<IActionResult> ExportDetailInCSV(int id)
        {
            if (_context.Risk == null) return NotFound();
            var risk = await _context.Risk
                .Where(x => x.Id == id)
                .Include(x => x.LinkedProducts).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers).ThenInclude(x => x.Supplier)
                .Include(x => x.Product).Include(x => x.Process)
                .Include(x => x.Asset).Include(x => x.Material).Include(x => x.Supplier)
                .FirstOrDefaultAsync();
            if (risk == null) return NotFound();

            AddAuditLog(risk.Id, "Exported (CSV)", risk.Status.ToString(), risk.Status.ToString());
            await _context.SaveChangesAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Risk ID,Title,Status,Category,Description,Risk Owner,Product,Process,Asset,Material,Supplier,Identified By,Identified On,Severity,Probability,Detectability,Initial RPN,Mitigation Strategy,Target Completion,Residual RPN,Risk Acceptable,Reviewed By,Reviewed On,Closed By,Closed On");
            builder.AppendLine($"{CsvSafe(risk.RiskId)},{CsvSafe(risk.Title)},{risk.Status},{risk.Category},{CsvSafe(risk.Description)},{CsvSafe(risk.RiskOwner)},{CsvSafe(JoinLinked(risk.LinkedProducts, x => x.Product?.Name, risk.Product?.Name))},{CsvSafe(JoinLinked(risk.LinkedProcesses, x => x.Process?.Name, risk.Process?.Name))},{CsvSafe(JoinLinked(risk.LinkedAssets, x => x.Asset?.Name, risk.Asset?.Name))},{CsvSafe(JoinLinked(risk.LinkedMaterials, x => x.Material?.Name, risk.Material?.Name))},{CsvSafe(JoinLinked(risk.LinkedSuppliers, x => x.Supplier?.LegalName, risk.Supplier?.LegalName))},{CsvSafe(risk.IdentifiedBy)},{risk.IdentifiedOn.ToSite().ToShortDateString()},{risk.Severity},{risk.Probability},{risk.Detectability},{risk.InitialRPN},{CsvSafe(risk.MitigationStrategy)},{risk.TargetCompletionDate?.ToShortDateString() ?? ""},{risk.ResidualRPN?.ToString() ?? ""},{(risk.RiskAcceptable.HasValue ? (risk.RiskAcceptable.Value ? "Yes" : "No") : "")},{CsvSafe(risk.ReviewedBy)},{risk.ReviewedOn.ToSite()?.ToShortDateString() ?? ""},{CsvSafe(risk.ClosedBy)},{risk.ClosedOn.ToSite()?.ToShortDateString() ?? ""}");

            return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"Risk-{risk.RiskId}.csv");
        }

        private bool RiskExists(int id) =>
            (_context.Risk?.Any(e => e.Id == id)).GetValueOrDefault();

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
            var risk = await _context.Risk.FindAsync(id);
            if (risk == null) return NotFound();

            if (risk.Status != Risk.RiskStatus.Closed)
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

            var fromStatus = risk.Status.ToString();
            risk.Status = Risk.RiskStatus.Mitigation;
            AddAuditLog(risk.Id, "Administrative Reopen", fromStatus, Risk.RiskStatus.Mitigation.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Record has been administratively reopened to Mitigation stage.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
