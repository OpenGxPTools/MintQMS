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
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,Auditor,ReadOnly")]
    public class AuditsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public AuditsController(ApplicationDbContext context, UserManager<AppUser> userManager, IPasswordHasher<AppUser> passwordHasher)
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
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.LegalName + " (" + x.SupplierCode + ")" })
                .ToList();
        }

        private async Task SyncAuditJunctions(int auditId,
            int[] productIds, int[] processIds, int[] assetIds,
            int[] materialIds, int[] supplierIds)
        {
            _context.AuditProduct.RemoveRange(
                await _context.AuditProduct.Where(x => x.AuditId == auditId).ToListAsync());
            _context.AuditProcess.RemoveRange(
                await _context.AuditProcess.Where(x => x.AuditId == auditId).ToListAsync());
            _context.AuditAsset.RemoveRange(
                await _context.AuditAsset.Where(x => x.AuditId == auditId).ToListAsync());
            _context.AuditMaterial.RemoveRange(
                await _context.AuditMaterial.Where(x => x.AuditId == auditId).ToListAsync());
            _context.AuditSupplier.RemoveRange(
                await _context.AuditSupplier.Where(x => x.AuditId == auditId).ToListAsync());
            foreach (var id in productIds.Distinct())
                _context.AuditProduct.Add(new AuditProduct { AuditId = auditId, ProductId = id });
            foreach (var id in processIds.Distinct())
                _context.AuditProcess.Add(new AuditProcess { AuditId = auditId, ProcessId = id });
            foreach (var id in assetIds.Distinct())
                _context.AuditAsset.Add(new AuditAsset { AuditId = auditId, AssetId = id });
            foreach (var id in materialIds.Distinct())
                _context.AuditMaterial.Add(new AuditMaterial { AuditId = auditId, MaterialId = id });
            foreach (var id in supplierIds.Distinct())
                _context.AuditSupplier.Add(new AuditSupplier { AuditId = auditId, SupplierId = id });
        }

        private static string JoinLinked<T>(ICollection<T>? junctions, Func<T, string?> getName, string? legacyFallback)
        {
            if (junctions?.Any() == true)
                return string.Join("; ", junctions.Select(getName).Where(n => !string.IsNullOrEmpty(n)));
            return legacyFallback ?? "";
        }

        private string GenerateAuditId()
        {
            var next = _context.Audit
                .Select(x => x.AuditId)
                .AsEnumerable()
                .Select(aid =>
                {
                    if (!string.IsNullOrEmpty(aid))
                    {
                        var parts = aid.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var n)) return n;
                    }
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max() + 1;
            return $"AUD-{next.ToString().PadLeft(4, '0')}";
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.AuditAuditLog.Add(new AuditAuditLog
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
            // 21 CFR Part 11 §11.100(a): signature must be from the currently logged-in user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrEmpty(pwd)) return false;
            return _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd) == PasswordVerificationResult.Success;
        }

        // GET: Audits/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var audits = await _context.Audit
                .Where(a => !a.IsDeleted)
                .Include(a => a.AuditLogs)
                .ToListAsync();

            ViewBag.Total = audits.Count;
            ViewBag.StatusCounts = Enum.GetValues<Audit.AuditStatus>()
                .ToDictionary(s => s.ToString(), s => audits.Count(a => a.Status == s));
            ViewBag.OpenCount = audits.Count(a => a.Status != Audit.AuditStatus.Closed);
            ViewBag.ClosedCount = audits.Count(a => a.Status == Audit.AuditStatus.Closed);

            // Findings summary across all audits
            ViewBag.TotalCriticalFindings = audits.Sum(a => a.CriticalFindings);
            ViewBag.TotalMajorFindings = audits.Sum(a => a.MajorFindings);
            ViewBag.TotalMinorFindings = audits.Sum(a => a.MinorFindings);
            ViewBag.CAPARequiredCount = audits.Count(a => a.CAPARequired && a.Status != Audit.AuditStatus.Closed);

            // Upcoming audits (planned, not yet started)
            ViewBag.Upcoming = audits
                .Where(a => a.Status == Audit.AuditStatus.Scheduled && a.PlannedStartDate.HasValue)
                .OrderBy(a => a.PlannedStartDate)
                .Take(10).ToList();

            // Overdue: scheduled but planned start already past
            ViewBag.Overdue = audits
                .Where(a => a.Status == Audit.AuditStatus.Scheduled
                    && a.PlannedStartDate.HasValue
                    && a.PlannedStartDate.Value.Date < SiteTime.Today)
                .OrderBy(a => a.PlannedStartDate)
                .Take(10).ToList();

            // Pending actions
            ViewBag.PendingReview = audits
                .Where(a => a.Status == Audit.AuditStatus.ReportDraft
                         || a.Status == Audit.AuditStatus.UnderReview
                         || a.Status == Audit.AuditStatus.Approved)
                .OrderBy(a => a.ScheduledOn)
                .Take(10).ToList();

            // Follow-up due
            var today = SiteTime.Today;
            ViewBag.FollowUpDue = audits
                .Where(a => !a.FollowUpCompleted && a.FollowUpDate.HasValue && a.FollowUpDate.Value.Date <= today.AddDays(14))
                .OrderBy(a => a.FollowUpDate)
                .Take(10).ToList();

            // Type distribution
            ViewBag.TypeCounts = Enum.GetValues<Audit.AuditType>()
                .ToDictionary(t => t.ToString(), t => audits.Count(a => a.Type == t));

            ViewBag.RecentActivity = audits
                .Where(a => a.AuditLogs != null)
                .SelectMany(a => a.AuditLogs!.Select(l => new { Log = l, Audit = a }))
                .OrderByDescending(x => x.Log.PerformedOn)
                .Take(12)
                .Select(x => new { x.Log, AuditId = x.Audit.AuditId, RecordId = x.Audit.Id, Title = x.Audit.Title })
                .ToList();

            return View();
        }

        // GET: Audits
        public async Task<IActionResult> Index()
        {
            var audits = await _context.Audit
                .Where(a => !a.IsDeleted)
                .Include(a => a.Product).Include(a => a.Process)
                .Include(a => a.Asset).Include(a => a.Material).Include(a => a.Supplier)
                .Include(a => a.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(a => a.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(a => a.LinkedAssets!).ThenInclude(x => x.Asset)
                .Include(a => a.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(a => a.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .OrderByDescending(a => a.ScheduledOn)
                .ToListAsync();
            return View(audits);
        }

        // GET: Audits/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Audit == null)
                return NotFound();

            var audit = await _context.Audit
                .Include(a => a.Product).Include(a => a.Process)
                .Include(a => a.Asset).Include(a => a.Material).Include(a => a.Supplier)
                .Include(a => a.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(a => a.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(a => a.LinkedAssets!).ThenInclude(x => x.Asset)
                .Include(a => a.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(a => a.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .Include(a => a.LinkedCapas!).ThenInclude(x => x.Capa)
                .Include(a => a.Attachments)
                .Include(a => a.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (audit == null)
                return NotFound();

            // Fallback for records created before junction migration
            if (audit.LinkedCapas?.Any() != true && audit.LinkedCapaId.HasValue)
                ViewBag.LinkedCapa = await _context.Capa!.FindAsync(audit.LinkedCapaId.Value);

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.AuditAttachment.DeletedOnly().Where(f => f.AuditId == id).ToListAsync();
            return View(audit);
        }

        // GET: Audits/Create
        public IActionResult Create()
        {
            PopulateViewData();
            return View();
        }

        // POST: Audits/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Title,Type,Scope,Description,PlannedStartDate,PlannedEndDate,AuditLocation,LeadAuditor,AuditTeam,Auditee,AuditeeContact,AuditPlan")] Audit audit,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            if (ModelState.IsValid)
            {
                audit.AuditId = GenerateAuditId();
                audit.ScheduledBy = _userManager.GetUserName(User) ?? "Unknown";
                audit.ScheduledOn = DateTime.UtcNow;
                audit.Status = Audit.AuditStatus.Scheduled;

                _context.Add(audit);
                await _context.SaveChangesAsync();

                await SyncAuditJunctions(audit.Id,
                    selectedProducts  ?? Array.Empty<int>(),
                    selectedProcesses ?? Array.Empty<int>(),
                    selectedAssets    ?? Array.Empty<int>(),
                    selectedMaterials ?? Array.Empty<int>(),
                    selectedSuppliers ?? Array.Empty<int>());

                AddAuditLog(audit.Id, "Created", null, audit.Status.ToString(),
                    $"ID: {audit.AuditId}; Title: {audit.Title}; Type: {audit.Type}; Scheduled by: {audit.ScheduledBy}");
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Audit {audit.AuditId} created.";
                return RedirectToAction(nameof(Details), new { id = audit.Id });
            }

            PopulateViewData();
            ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
            ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
            ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
            ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
            ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            return View(audit);
        }

        // GET: Audits/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Audit == null)
                return NotFound();

            var audit = await _context.Audit
                .Include(a => a.Product).Include(a => a.Process)
                .Include(a => a.Asset).Include(a => a.Material).Include(a => a.Supplier)
                .Include(a => a.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(a => a.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(a => a.LinkedAssets!).ThenInclude(x => x.Asset)
                .Include(a => a.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(a => a.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null)
                return NotFound();

            if (audit.Status == Audit.AuditStatus.UnderReview ||
                audit.Status == Audit.AuditStatus.Approved ||
                audit.Status == Audit.AuditStatus.Closed)
                return Forbid();

            ViewBag.IsScheduledStage = audit.Status == Audit.AuditStatus.Scheduled;

            ViewBag.SelectedProductIds = audit.LinkedProducts?.Any() == true
                ? audit.LinkedProducts.Select(x => x.ProductId).ToList()
                : (audit.ProductId.HasValue ? new List<int> { audit.ProductId.Value } : new List<int>());
            ViewBag.SelectedProcessIds = audit.LinkedProcesses?.Any() == true
                ? audit.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (audit.ProcessId.HasValue ? new List<int> { audit.ProcessId.Value } : new List<int>());
            ViewBag.SelectedAssetIds = audit.LinkedAssets?.Any() == true
                ? audit.LinkedAssets.Select(x => x.AssetId).ToList()
                : (audit.AssetId.HasValue ? new List<int> { audit.AssetId.Value } : new List<int>());
            ViewBag.SelectedMaterialIds = audit.LinkedMaterials?.Any() == true
                ? audit.LinkedMaterials.Select(x => x.MaterialId).ToList()
                : (audit.MaterialId.HasValue ? new List<int> { audit.MaterialId.Value } : new List<int>());
            ViewBag.SelectedSupplierIds = audit.LinkedSuppliers?.Any() == true
                ? audit.LinkedSuppliers.Select(x => x.SupplierId).ToList()
                : (audit.SupplierId.HasValue ? new List<int> { audit.SupplierId.Value } : new List<int>());

            PopulateViewData();
            return View(audit);
        }

        // POST: Audits/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedAssets,
            int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            var snap  = await _context.Audit.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            var audit = await _context.Audit.FindAsync(id);
            if (audit == null || snap == null) return NotFound();

            if (audit.Status == Audit.AuditStatus.UnderReview ||
                audit.Status == Audit.AuditStatus.Approved ||
                audit.Status == Audit.AuditStatus.Closed)
                return Forbid();

            var isScheduledStage = audit.Status == Audit.AuditStatus.Scheduled;

            bool updated;
            if (isScheduledStage)
            {
                updated = await TryUpdateModelAsync(audit, "",
                    a => a.Title, a => a.Type, a => a.Scope, a => a.Description,
                    a => a.PlannedStartDate, a => a.PlannedEndDate,
                    a => a.AuditLocation, a => a.LeadAuditor, a => a.AuditTeam,
                    a => a.Auditee, a => a.AuditeeContact, a => a.AuditPlan);
            }
            else // InProgress or ReportDraft
            {
                updated = await TryUpdateModelAsync(audit, "",
                    a => a.Title, a => a.Type, a => a.Scope, a => a.Description,
                    a => a.PlannedStartDate, a => a.PlannedEndDate,
                    a => a.ActualStartDate, a => a.ActualEndDate,
                    a => a.AuditLocation, a => a.LeadAuditor, a => a.AuditTeam,
                    a => a.Auditee, a => a.AuditeeContact,
                    a => a.FindingsSummary, a => a.CriticalFindings, a => a.MajorFindings,
                    a => a.MinorFindings, a => a.Observations, a => a.OverallRating,
                    a => a.CAPARequired, a => a.CorrectiveAction, a => a.LinkedCapaId,
                    a => a.FollowUpDate, a => a.FollowUpCompleted,
                    a => a.AuditPlan, a => a.AuditReport,
                    a => a.Recommendations, a => a.ManagementResponse);
            }

            if (updated && ModelState.IsValid)
            {
                // Capture affected-item associations before sync (for the audit diff)
                var oldAuProductIds  = await _context.AuditProduct.Where(x => x.AuditId == audit.Id).Select(x => x.ProductId).ToListAsync();
                var oldAuProcessIds  = await _context.AuditProcess.Where(x => x.AuditId == audit.Id).Select(x => x.ProcessId).ToListAsync();
                var oldAuAssetIds    = await _context.AuditAsset.Where(x => x.AuditId == audit.Id).Select(x => x.AssetId).ToListAsync();
                var oldAuMaterialIds = await _context.AuditMaterial.Where(x => x.AuditId == audit.Id).Select(x => x.MaterialId).ToListAsync();
                var oldAuSupplierIds = await _context.AuditSupplier.Where(x => x.AuditId == audit.Id).Select(x => x.SupplierId).ToListAsync();

                if (isScheduledStage)
                {
                    // Clear legacy FK columns — junctions now own the relationship
                    audit.ProductId  = null;
                    audit.ProcessId  = null;
                    audit.AssetId    = null;
                    audit.MaterialId = null;
                    audit.SupplierId = null;

                    await SyncAuditJunctions(audit.Id,
                        selectedProducts  ?? Array.Empty<int>(),
                        selectedProcesses ?? Array.Empty<int>(),
                        selectedAssets    ?? Array.Empty<int>(),
                        selectedMaterials ?? Array.Empty<int>(),
                        selectedSuppliers ?? Array.Empty<int>());
                }

                try
                {
                    var diff = new List<string>();
                    AuditDiffHelper.Track(diff,     "Title",              snap.Title,              audit.Title);
                    AuditDiffHelper.TrackEnum(diff, "Type",               snap.Type,                audit.Type);
                    AuditDiffHelper.TrackEnum(diff, "Scope",              snap.Scope,               audit.Scope);
                    AuditDiffHelper.Track(diff,     "Description",        snap.Description,         audit.Description);
                    AuditDiffHelper.TrackDate(diff, "Planned Start",      snap.PlannedStartDate,    audit.PlannedStartDate);
                    AuditDiffHelper.TrackDate(diff, "Planned End",        snap.PlannedEndDate,      audit.PlannedEndDate);
                    AuditDiffHelper.TrackDate(diff, "Actual Start",       snap.ActualStartDate,     audit.ActualStartDate);
                    AuditDiffHelper.TrackDate(diff, "Actual End",         snap.ActualEndDate,       audit.ActualEndDate);
                    AuditDiffHelper.Track(diff,     "Location",           snap.AuditLocation,       audit.AuditLocation);
                    AuditDiffHelper.Track(diff,     "Lead Auditor",       snap.LeadAuditor,         audit.LeadAuditor);
                    AuditDiffHelper.Track(diff,     "Audit Team",         snap.AuditTeam,           audit.AuditTeam);
                    AuditDiffHelper.Track(diff,     "Auditee",            snap.Auditee,             audit.Auditee);
                    AuditDiffHelper.Track(diff,     "Auditee Contact",    snap.AuditeeContact,      audit.AuditeeContact);
                    AuditDiffHelper.Track(diff,     "Findings Summary",   snap.FindingsSummary,     audit.FindingsSummary);
                    AuditDiffHelper.Track<int>(diff, "Critical Findings", snap.CriticalFindings,    audit.CriticalFindings);
                    AuditDiffHelper.Track<int>(diff, "Major Findings",    snap.MajorFindings,       audit.MajorFindings);
                    AuditDiffHelper.Track<int>(diff, "Minor Findings",    snap.MinorFindings,       audit.MinorFindings);
                    AuditDiffHelper.Track<int>(diff, "Observations",      snap.Observations,        audit.Observations);
                    AuditDiffHelper.TrackEnum(diff, "Overall Rating",     snap.OverallRating,       audit.OverallRating);
                    AuditDiffHelper.Track(diff,     "Corrective Action",  snap.CorrectiveAction,    audit.CorrectiveAction);
                    AuditDiffHelper.Track(diff,     "Recommendations",    snap.Recommendations,     audit.Recommendations);
                    AuditDiffHelper.Track(diff,     "Mgmt Response",      snap.ManagementResponse,  audit.ManagementResponse);
                    AuditDiffHelper.Track(diff,     "Audit Plan",         snap.AuditPlan,           audit.AuditPlan);
                    AuditDiffHelper.Track(diff,     "Audit Report",       snap.AuditReport,         audit.AuditReport);
                    AuditDiffHelper.TrackDate(diff, "Follow-up Date",     snap.FollowUpDate,        audit.FollowUpDate);
                    if (isScheduledStage)
                    {
                        AuditDiffHelper.TrackSet(diff, "Affected Products",  oldAuProductIds,  selectedProducts  ?? Array.Empty<int>(),
                            ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Processes", oldAuProcessIds,  selectedProcesses ?? Array.Empty<int>(),
                            ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Assets",    oldAuAssetIds,    selectedAssets    ?? Array.Empty<int>(),
                            ids => _context.Asset.Where(a => ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Materials", oldAuMaterialIds, selectedMaterials ?? Array.Empty<int>(),
                            ids => _context.Material.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Suppliers", oldAuSupplierIds, selectedSuppliers ?? Array.Empty<int>(),
                            ids => _context.Supplier.Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id, s => s.LegalName));
                    }
                    if (diff.Count > 0) AddAuditLog(audit.Id, "Edited", null, null, AuditDiffHelper.Build(diff));
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AuditExists(audit.Id)) return NotFound();
                    else throw;
                }

                TempData["Success"] = "Audit updated.";
                return RedirectToAction(nameof(Details), new { id = audit.Id });
            }

            PopulateViewData();
            ViewBag.IsScheduledStage = isScheduledStage;
            if (isScheduledStage)
            {
                ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
                ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
                ViewBag.SelectedAssetIds    = selectedAssets?.ToList()    ?? new List<int>();
                ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
                ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            }
            return View(audit);
        }

        // GET: Audits/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Audit == null)
                return NotFound();

            var audit = await _context.Audit
                .Include(a => a.Product).Include(a => a.Process)
                .Include(a => a.Asset).Include(a => a.Material).Include(a => a.Supplier)
                .Include(a => a.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(a => a.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(a => a.LinkedAssets!).ThenInclude(x => x.Asset)
                .Include(a => a.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(a => a.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (audit == null)
                return NotFound();

            if (audit.Status != Audit.AuditStatus.Scheduled)
                return Forbid();

            return View(audit);
        }

        // POST: Audits/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var audit = await _context.Audit
                .Include(a => a.Attachments)
                .Include(a => a.AuditLogs)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit != null)
            {
                if (audit.Status != Audit.AuditStatus.Scheduled)
                    return Forbid();

                // Attachments (rows and files) are retained with the soft-deleted record.
                AddAuditLog(audit.Id, "Deleted", audit.Status.ToString(), null, audit.Title);
                audit.IsDeleted = true;
                audit.DeletedBy = _userManager.GetUserName(User);
                audit.DeletedOn = DateTime.UtcNow;
                _context.Audit.Update(audit);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Workflow transitions — all POST

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,Auditor")]
        public async Task<IActionResult> InProgress(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Transition was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();
            if (audit.Status != Audit.AuditStatus.Scheduled)
            {
                TempData["WorkflowError"] = "Audit must be in Scheduled status to start.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = audit.Status.ToString();
            audit.ConductedBy = _userManager.GetUserName(User);
            audit.ConductedOn = DateTime.UtcNow;
            audit.ActualStartDate ??= DateTime.UtcNow;
            audit.Status = Audit.AuditStatus.InProgress;

            AddAuditLog(audit.Id, "Started (In Progress)", from, audit.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Audit started.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,Auditor")]
        public async Task<IActionResult> ReportDraft(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Transition was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();
            if (audit.Status != Audit.AuditStatus.InProgress)
            {
                TempData["WorkflowError"] = "Audit must be In Progress to submit a report draft.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = audit.Status.ToString();
            audit.ActualEndDate ??= DateTime.UtcNow;
            audit.Status = Audit.AuditStatus.ReportDraft;

            AddAuditLog(audit.Id, "Report Draft submitted", from, audit.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Report draft submitted.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,Auditor")]
        public async Task<IActionResult> UnderReview(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Transition was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();
            if (audit.Status != Audit.AuditStatus.ReportDraft)
            {
                TempData["WorkflowError"] = "Audit must be in Report Draft status to submit for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = audit.Status.ToString();
            audit.ReviewedBy = _userManager.GetUserName(User);
            audit.ReviewedOn = DateTime.UtcNow;
            audit.Status = Audit.AuditStatus.UnderReview;

            AddAuditLog(audit.Id, "Submitted for Review", from, audit.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Audit submitted for review.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager")]
        public async Task<IActionResult> Approved(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Transition was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();
            if (audit.Status != Audit.AuditStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Audit must be Under Review to be approved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = audit.Status.ToString();
            audit.ApprovedBy = _userManager.GetUserName(User);
            audit.ApprovedOn = DateTime.UtcNow;
            audit.Status = Audit.AuditStatus.Approved;

            AddAuditLog(audit.Id, "Approved", from, audit.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Audit approved.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager")]
        public async Task<IActionResult> Closed(int id, string email, string pwd, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Transition was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();
            if (audit.Status != Audit.AuditStatus.Approved)
            {
                TempData["WorkflowError"] = "Audit must be Approved before it can be closed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = audit.Status.ToString();
            audit.ClosedBy = _userManager.GetUserName(User);
            audit.ClosedOn = DateTime.UtcNow;
            audit.Status = Audit.AuditStatus.Closed;
            var _auditSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            audit.RetainUntil = audit.ClosedOn.Value.AddYears(_auditSets.AuditRetentionYears);

            AddAuditLog(audit.Id, "Closed", from, audit.Status.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Audit closed.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Audits/ReturnForRevision — QM rejects Under Review, sends back to Report Draft
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager")]
        public async Task<IActionResult> ReturnForRevision(int id, string email, string pwd, string returnReason, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Return was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(returnReason))
            {
                TempData["WorkflowError"] = "A reason for return is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();
            if (audit.Status != Audit.AuditStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Audit must be Under Review to return for revision.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = audit.Status.ToString();
            audit.ReviewedBy = null;
            audit.ReviewedOn = null;
            audit.Status = Audit.AuditStatus.ReportDraft;

            AddAuditLog(audit.Id, "Returned for Revision", from, audit.Status.ToString(),
                string.IsNullOrWhiteSpace(notes) ? returnReason : $"{returnReason} | {notes}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Audit returned to Report Draft for revision.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Audits/ReturnToReview — QM revokes approval, sends back to Under Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager")]
        public async Task<IActionResult> ReturnToReview(int id, string email, string pwd, string returnReason, string? notes)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Return was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(returnReason))
            {
                TempData["WorkflowError"] = "A reason for return is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();
            if (audit.Status != Audit.AuditStatus.Approved)
            {
                TempData["WorkflowError"] = "Audit must be Approved to return to review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var from = audit.Status.ToString();
            audit.ApprovedBy = null;
            audit.ApprovedOn = null;
            audit.Status = Audit.AuditStatus.UnderReview;

            AddAuditLog(audit.Id, "Returned to Review", from, audit.Status.ToString(),
                string.IsNullOrWhiteSpace(notes) ? returnReason : $"{returnReason} | {notes}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Audit returned to Under Review.";
            TempData["ActiveTab"] = "auditlog";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Attachments

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int auditId, IFormFile file, string? description)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = auditId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, Excel, ZIP, JPG, PNG.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = auditId });
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File size exceeds the 25 MB limit.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = auditId });
            }

            var audit = await _context.Audit.FindAsync(auditId);
            if (audit == null) return NotFound();
            if (audit.Status == Audit.AuditStatus.Closed)
            {
                TempData["Error"] = "Attachments cannot be added to a closed audit.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = auditId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audits", auditId.ToString());
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

            _context.AuditAttachment.Add(new AuditAttachment
            {
                AuditId = auditId,
                FileName = file.FileName,
                FilePath = $"/uploads/audits/{auditId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                Description = description
            });

            AddAuditLog(auditId, "Attachment uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded.";
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = auditId });
        }

        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();
            var attachment = await _context.AuditAttachment.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.AuditId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.AuditId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.AuditId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Audit",
                attachment.AuditId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.AuditAttachment.FindAsync(id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.AuditId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.AuditId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.AuditId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Audit",
                attachment.AuditId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int auditId, string? deletionReason)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var attachment = await _context.AuditAttachment.FindAsync(id);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                auditId = attachment.AuditId;
                var parentAudit = await _context.Audit.FindAsync(auditId);
                if (parentAudit?.Status == Audit.AuditStatus.Closed) return Forbid();

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = auditId });
                }

                // Soft delete: the row and file are retained for traceability.
                attachment.IsDeleted      = true;
                attachment.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                AddAuditLog(auditId, "Attachment deleted", null, null, $"{attachment.FileName} — Reason: {attachment.DeletionReason}");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attachment deleted.";
            }

            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = auditId });
        }

        // Create CAPA from Audit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCapa(int? id)
        {
            if (id == null) return NotFound();
            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();

            if (audit.Status == Audit.AuditStatus.Scheduled || audit.Status == Audit.AuditStatus.Closed)
            {
                TempData["Error"] = "CAPA can only be created once the audit is underway and not yet closed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (audit.LinkedCapaId.HasValue)
            {
                TempData["Error"] = "A CAPA is already linked to this audit.";
                return RedirectToAction(nameof(Details), new { id });
            }

            audit.CAPARequired = true;
            _context.Update(audit);
            await _context.SaveChangesAsync();

            return RedirectToAction("Create", "Capas", new { auditId = id });
        }

        // Export
        public async Task<IActionResult> ExportRegister()
        {
            var audits = await _context.Audit
                .Include(a => a.Product).Include(a => a.Process)
                .Include(a => a.Asset).Include(a => a.Material).Include(a => a.Supplier)
                .Include(a => a.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(a => a.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(a => a.LinkedAssets!).ThenInclude(x => x.Asset)
                .Include(a => a.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(a => a.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .OrderByDescending(a => a.ScheduledOn)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Audit Register");

            var headers = new[] { "Audit ID", "Title", "Type", "Scope", "Status", "Lead Auditor", "Auditee",
                "Planned Start", "Planned End", "Actual Start", "Actual End", "Rating",
                "Critical Findings", "Major Findings", "Minor Findings", "Observations",
                "CAPA Required", "Linked CAPA ID", "Follow-Up Date", "Scheduled By", "Product", "Process", "Supplier" };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            ws.SheetView.FreezeRows(1);

            for (int i = 0; i < audits.Count; i++)
            {
                var a = audits[i];
                var row = i + 2;
                var fill = i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#F2F2F2");
                ws.Cell(row, 1).Value = a.AuditId;
                ws.Cell(row, 2).Value = a.Title;
                ws.Cell(row, 3).Value = a.Type.ToString();
                ws.Cell(row, 4).Value = a.Scope.ToString();
                ws.Cell(row, 5).Value = a.Status.ToString();
                ws.Cell(row, 6).Value = a.LeadAuditor ?? "";
                ws.Cell(row, 7).Value = a.Auditee ?? "";
                ws.Cell(row, 8).Value = a.PlannedStartDate?.ToShortDateString() ?? "";
                ws.Cell(row, 9).Value = a.PlannedEndDate?.ToShortDateString() ?? "";
                ws.Cell(row, 10).Value = a.ActualStartDate?.ToShortDateString() ?? "";
                ws.Cell(row, 11).Value = a.ActualEndDate?.ToShortDateString() ?? "";
                ws.Cell(row, 12).Value = a.OverallRating?.ToString() ?? "";
                ws.Cell(row, 13).Value = a.CriticalFindings;
                ws.Cell(row, 14).Value = a.MajorFindings;
                ws.Cell(row, 15).Value = a.MinorFindings;
                ws.Cell(row, 16).Value = a.Observations;
                ws.Cell(row, 17).Value = a.CAPARequired ? "Yes" : "No";
                ws.Cell(row, 18).Value = a.LinkedCapaId?.ToString() ?? "";
                ws.Cell(row, 19).Value = a.FollowUpDate?.ToShortDateString() ?? "";
                ws.Cell(row, 20).Value = a.ScheduledBy;
                ws.Cell(row, 21).Value = JoinLinked(a.LinkedProducts,  x => x.Product?.Name,       a.Product?.Name);
                ws.Cell(row, 22).Value = JoinLinked(a.LinkedProcesses, x => x.Process?.Name,      a.Process?.Name);
                ws.Cell(row, 23).Value = JoinLinked(a.LinkedSuppliers, x => x.Supplier?.LegalName, a.Supplier?.LegalName);
                for (int c = 1; c <= headers.Length; c++)
                    ws.Cell(row, c).Style.Fill.BackgroundColor = fill;
            }

            ws.Columns().AdjustToContents();

            OpenQMS.Services.ExcelExportHelper.AddExportInfoSheet(wb, _userManager.GetUserName(User) ?? "Unknown", "Audit Register", audits.Count);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            // Register exports are recorded in the workbook's Export Info sheet (who / when / count).
            // No per-record audit entry: there is no parent record, and id 0 violates the audit-log FK.

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"AuditRegister_{SiteTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> ExportAuditDetail(int id)
        {
            if (_context.Audit == null) return NotFound();
            var audit = await _context.Audit
                .Where(x => x.Id == id)
                .Include(x => x.Product).Include(x => x.Process)
                .Include(x => x.Asset).Include(x => x.Material).Include(x => x.Supplier)
                .Include(x => x.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(x => x.LinkedAssets!).ThenInclude(x => x.Asset)
                .Include(x => x.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync();
            if (audit == null) return NotFound();

            var fields = new List<(string, string?)>
            {
                ("Audit ID",          audit.AuditId),
                ("Title",             audit.Title),
                ("Status",            audit.Status.ToString()),
                ("Type",              audit.Type.ToString()),
                ("Scope",             audit.Scope.ToString()),
                ("Description",       audit.Description),
                ("Audit Location",    audit.AuditLocation),
                ("Lead Auditor",      audit.LeadAuditor),
                ("Audit Team",        audit.AuditTeam),
                ("Auditee",           audit.Auditee),
                ("Auditee Contact",   audit.AuditeeContact),
                ("Planned Start",     audit.PlannedStartDate?.ToShortDateString()),
                ("Planned End",       audit.PlannedEndDate?.ToShortDateString()),
                ("Actual Start",      audit.ActualStartDate?.ToShortDateString()),
                ("Actual End",        audit.ActualEndDate?.ToShortDateString()),
                ("Scheduled By",      audit.ScheduledBy),
                ("Conducted By",      audit.ConductedBy),
                ("Critical Findings", audit.CriticalFindings.ToString()),
                ("Major Findings",    audit.MajorFindings.ToString()),
                ("Minor Findings",    audit.MinorFindings.ToString()),
                ("Observations",      audit.Observations.ToString()),
                ("Products",          JoinLinked(audit.LinkedProducts,  x => x.Product?.Name,       audit.Product?.Name)),
                ("Processes",         JoinLinked(audit.LinkedProcesses, x => x.Process?.Name,      audit.Process?.Name)),
                ("Assets",            JoinLinked(audit.LinkedAssets,    x => x.Asset?.Name,        audit.Asset?.Name)),
                ("Materials",         JoinLinked(audit.LinkedMaterials, x => x.Material?.Name,     audit.Material?.Name)),
                ("Suppliers",         JoinLinked(audit.LinkedSuppliers, x => x.Supplier?.LegalName, audit.Supplier?.LegalName)),
                ("Overall Rating",    audit.OverallRating?.ToString()),
                ("Corrective Action", audit.CorrectiveAction),
                ("CAPA Required",     audit.CAPARequired ? "Yes" : "No"),
                ("Reviewed By",       audit.ReviewedBy),
                ("Approved By",       audit.ApprovedBy),
                ("Closed By",         audit.ClosedBy),
            };

            AddAuditLog(audit.Id, "Exported (PDF)", audit.Status.ToString(), audit.Status.ToString());
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Audit Detail Report", audit.AuditId, audit.Status.ToString(), fields,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Audit-{audit.AuditId}.pdf");
        }

        public async Task<IActionResult> ExportDetailInCSV(int id)
        {
            if (_context.Audit == null) return NotFound();
            var audit = await _context.Audit
                .Where(x => x.Id == id)
                .Include(x => x.Product).Include(x => x.Process)
                .Include(x => x.Asset).Include(x => x.Material).Include(x => x.Supplier)
                .FirstOrDefaultAsync();
            if (audit == null) return NotFound();

            AddAuditLog(audit.Id, "Exported (CSV)", audit.Status.ToString(), audit.Status.ToString());
            await _context.SaveChangesAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Audit ID,Title,Status,Type,Scope,Lead Auditor,Planned Start,Planned End,Actual Start,Actual End,Critical Findings,Major Findings,Minor Findings,Observations,Overall Rating,CAPA Required,Reviewed By,Approved By,Closed By");
            builder.AppendLine($"{CsvSafe(audit.AuditId)},{CsvSafe(audit.Title)},{audit.Status},{audit.Type},{audit.Scope},{CsvSafe(audit.LeadAuditor)},{audit.PlannedStartDate?.ToShortDateString() ?? ""},{audit.PlannedEndDate?.ToShortDateString() ?? ""},{audit.ActualStartDate?.ToShortDateString() ?? ""},{audit.ActualEndDate?.ToShortDateString() ?? ""},{audit.CriticalFindings},{audit.MajorFindings},{audit.MinorFindings},{audit.Observations},{audit.OverallRating?.ToString() ?? ""},{(audit.CAPARequired ? "Yes" : "No")},{CsvSafe(audit.ReviewedBy)},{CsvSafe(audit.ApprovedBy)},{CsvSafe(audit.ClosedBy)}");

            return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"Audit-{audit.AuditId}.csv");
        }

        private bool AuditExists(int id) =>
            (_context.Audit?.Any(e => e.Id == id)).GetValueOrDefault();

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
            var audit = await _context.Audit.FindAsync(id);
            if (audit == null) return NotFound();

            if (audit.Status != Audit.AuditStatus.Closed)
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

            var fromStatus = audit.Status.ToString();
            audit.Status = Audit.AuditStatus.Approved;
            AddAuditLog(audit.Id, "Administrative Reopen", fromStatus, Audit.AuditStatus.Approved.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Record has been administratively reopened to Approved stage.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
