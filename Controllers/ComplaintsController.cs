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
    [Authorize]
    public class ComplaintsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public ComplaintsController(ApplicationDbContext context, UserManager<AppUser> userManager, IPasswordHasher<AppUser> passwordHasher)
        {
            _context = context;
            _userManager = userManager;
            _passwordHasher = passwordHasher;
        }

        private void PopulateViewData(int? selectedCustomerId = null)
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
            ViewData["CustomerId"] = new SelectList(
                _context.Customer.Where(x => !x.IsDeleted && x.Status == Customer.CustomerStatus.Approved).OrderBy(c => c.LegalName),
                "Id", "LegalName", selectedCustomerId);
        }

        private async Task SyncComplaintJunctions(int complaintId,
            int[] productIds, int[] processIds, int[] materialIds, int[] supplierIds)
        {
            _context.ComplaintProduct.RemoveRange(
                await _context.ComplaintProduct.Where(x => x.ComplaintId == complaintId).ToListAsync());
            _context.ComplaintProcess.RemoveRange(
                await _context.ComplaintProcess.Where(x => x.ComplaintId == complaintId).ToListAsync());
            _context.ComplaintMaterial.RemoveRange(
                await _context.ComplaintMaterial.Where(x => x.ComplaintId == complaintId).ToListAsync());
            _context.ComplaintSupplier.RemoveRange(
                await _context.ComplaintSupplier.Where(x => x.ComplaintId == complaintId).ToListAsync());
            foreach (var id in productIds.Distinct())
                _context.ComplaintProduct.Add(new ComplaintProduct { ComplaintId = complaintId, ProductId = id });
            foreach (var id in processIds.Distinct())
                _context.ComplaintProcess.Add(new ComplaintProcess { ComplaintId = complaintId, ProcessId = id });
            foreach (var id in materialIds.Distinct())
                _context.ComplaintMaterial.Add(new ComplaintMaterial { ComplaintId = complaintId, MaterialId = id });
            foreach (var id in supplierIds.Distinct())
                _context.ComplaintSupplier.Add(new ComplaintSupplier { ComplaintId = complaintId, SupplierId = id });
        }

        private static string JoinLinked<T>(ICollection<T>? junctions, Func<T, string?> getName, string? legacyFallback)
        {
            if (junctions?.Any() == true)
                return string.Join("; ", junctions.Select(getName).Where(n => !string.IsNullOrEmpty(n)));
            return legacyFallback ?? "";
        }

        private string GenerateComplaintId()
        {
            var allIds = _context.Complaint
                .AsEnumerable()
                .Select(x =>
                {
                    if (!string.IsNullOrEmpty(x.ComplaintId))
                    {
                        var parts = x.ComplaintId.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var n)) return n;
                    }
                    return 0;
                });
            var next = (allIds.Any() ? allIds.Max() : 0) + 1;
            return $"CMP-{next.ToString().PadLeft(4, '0')}";
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.ComplaintAuditLog.Add(new ComplaintAuditLog
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

        // GET: Complaints/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var complaints = await _context.Complaint
                .Include(c => c.Customer)
                .Include(c => c.AuditLogs)
                .ToListAsync();

            ViewBag.Total = complaints.Count;
            var workflowOrder = new[]
            {
                Complaint.ComplaintStatus.Received,
                Complaint.ComplaintStatus.Evaluation,
                Complaint.ComplaintStatus.PendingAcceptance,
                Complaint.ComplaintStatus.Accepted,
                Complaint.ComplaintStatus.Resolution,
                Complaint.ComplaintStatus.Closed
            };
            ViewBag.StatusCounts = workflowOrder.ToDictionary(
                s => s == Complaint.ComplaintStatus.PendingAcceptance ? "Pending Acceptance" : s.ToString(),
                s => complaints.Count(c => c.Status == s));
            ViewBag.OpenCount = complaints.Count(c => c.Status != Complaint.ComplaintStatus.Closed);
            ViewBag.ClosedCount = complaints.Count(c => c.Status == Complaint.ComplaintStatus.Closed);
            ViewBag.CriticalCount = complaints.Count(c => c.Severity == Complaint.ComplaintSeverity.Critical && c.Status != Complaint.ComplaintStatus.Closed);

            ViewBag.SeverityCounts = Enum.GetValues<Complaint.ComplaintSeverity>()
                .ToDictionary(s => s.ToString(), s => complaints.Count(c => c.Severity == s));

            var cutoff = SiteTime.Today.AddDays(-30);
            ViewBag.Aged = complaints
                .Where(c => c.Status != Complaint.ComplaintStatus.Closed && c.ReceivedOn.Date < cutoff)
                .OrderBy(c => c.ReceivedOn)
                .Take(10).ToList();

            ViewBag.PendingEvaluation = complaints
                .Where(c => c.Status == Complaint.ComplaintStatus.Received)
                .OrderBy(c => c.ReceivedOn).Take(10).ToList();

            ViewBag.PendingAcceptanceQueue = complaints
                .Where(c => c.Status == Complaint.ComplaintStatus.PendingAcceptance)
                .OrderBy(c => c.ReceivedOn).Take(10).ToList();

            ViewBag.Critical = complaints
                .Where(c => c.Severity == Complaint.ComplaintSeverity.Critical && c.Status != Complaint.ComplaintStatus.Closed)
                .OrderBy(c => c.ReceivedOn).Take(10).ToList();

            ViewBag.RecentActivity = complaints
                .Where(c => c.AuditLogs != null)
                .SelectMany(c => c.AuditLogs!.Select(l => new { Log = l, Complaint = c }))
                .OrderByDescending(x => x.Log.PerformedOn)
                .Take(12)
                .Select(x => new { x.Log, ComplaintId = x.Complaint.ComplaintId, RecordId = x.Complaint.Id, Title = x.Complaint.Title })
                .ToList();

            return View();
        }

        // GET: Complaints
        public async Task<IActionResult> Index()
        {
            var complaints = await _context.Complaint
                .Where(c => !c.IsDeleted)
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Customer)
                .Include(c => c.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(c => c.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .OrderByDescending(c => c.ReceivedOn)
                .ToListAsync();
            return View(complaints);
        }

        // GET: Complaints/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Complaint == null)
                return NotFound();

            var complaint = await _context.Complaint
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Customer)
                .Include(c => c.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(c => c.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .Include(c => c.LinkedCapas!).ThenInclude(x => x.Capa)
                .Include(c => c.Attachments)
                .Include(c => c.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (complaint == null)
                return NotFound();

            ViewData["LinkedAEs"] = await _context.AdverseEvent
                .Include(a => a.LinkedCapa)
                .Where(a => a.LinkedComplaintId == id)
                .OrderByDescending(a => a.Id)
                .ToListAsync();

            // Unlinked, non-voided adverse events available to link to this complaint
            if (complaint.Status != Complaint.ComplaintStatus.Closed)
            {
                ViewData["AvailableAEs"] = await _context.AdverseEvent
                    .Where(a => a.LinkedComplaintId == null && a.Status != AdverseEvent.AEStatus.Voided)
                    .OrderByDescending(a => a.Id)
                    .Select(a => new { a.Id, Label = a.AENumber + (a.IsSerious ? " (Serious)" : "") })
                    .ToListAsync();
            }

            // Fallback for records created before junction migration
            if (complaint.LinkedCapas?.Any() != true && complaint.LinkedCapaId.HasValue)
                ViewBag.LinkedCapa = await _context.Capa.FindAsync(complaint.LinkedCapaId.Value);

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.ComplaintAttachment.DeletedOnly().Where(f => f.ComplaintId == id).ToListAsync();
            return View(complaint);
        }

        // GET: Complaints/Create
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public IActionResult Create()
        {
            PopulateViewData();
            return View();
        }

        // POST: Complaints/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> Create(
            [Bind("Title,CustomerId,CustomerName,CustomerContact,CustomerReference,Description,Severity")] Complaint complaint,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            if (ModelState.IsValid)
            {
                complaint.ComplaintId = GenerateComplaintId();
                complaint.ReceivedBy = _userManager.GetUserName(User) ?? "Unknown";
                complaint.ReceivedOn = DateTime.UtcNow;
                complaint.Status = Complaint.ComplaintStatus.Received;

                var settings = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
                int resolutionDays = complaint.Severity == Complaint.ComplaintSeverity.Critical
                    ? settings.CriticalComplaintResolutionDays
                    : settings.DefaultComplaintResolutionDays;
                complaint.TargetResolutionDate = complaint.ReceivedOn.AddDays(resolutionDays);

                _context.Add(complaint);
                await _context.SaveChangesAsync();

                await SyncComplaintJunctions(complaint.Id,
                    selectedProducts  ?? Array.Empty<int>(),
                    selectedProcesses ?? Array.Empty<int>(),
                    selectedMaterials ?? Array.Empty<int>(),
                    selectedSuppliers ?? Array.Empty<int>());

                AddAuditLog(complaint.Id, "Created", null, Complaint.ComplaintStatus.Received.ToString(),
                    $"ID: {complaint.ComplaintId}; Title: {complaint.Title}; Severity: {complaint.Severity}; Received by: {complaint.ReceivedBy}");
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Complaint {complaint.ComplaintId} created successfully.";
                return RedirectToAction(nameof(Details), new { id = complaint.Id });
            }

            PopulateViewData(complaint.CustomerId);
            ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
            ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
            ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
            ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            return View(complaint);
        }

        // GET: Complaints/Edit/5
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Complaint == null)
                return NotFound();

            if (User.IsInRole(Constants.ReadOnlyRole))
                return Forbid();

            var complaint = await _context.Complaint
                .Include(c => c.Product)
                .Include(c => c.Process)
                .Include(c => c.Material)
                .Include(c => c.Supplier)
                .Include(c => c.Customer)
                .Include(c => c.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(c => c.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (complaint == null)
                return NotFound();

            // Editing not allowed at PendingAcceptance, Resolution, or Closed
            if (complaint.Status == Complaint.ComplaintStatus.PendingAcceptance ||
                complaint.Status == Complaint.ComplaintStatus.Resolution ||
                complaint.Status == Complaint.ComplaintStatus.Closed)
                return Forbid();

            ViewBag.SelectedProductIds = complaint.LinkedProducts?.Any() == true
                ? complaint.LinkedProducts.Select(x => x.ProductId).ToList()
                : (complaint.ProductId.HasValue ? new List<int> { complaint.ProductId.Value } : new List<int>());
            ViewBag.SelectedProcessIds = complaint.LinkedProcesses?.Any() == true
                ? complaint.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (complaint.ProcessId.HasValue ? new List<int> { complaint.ProcessId.Value } : new List<int>());
            ViewBag.SelectedMaterialIds = complaint.LinkedMaterials?.Any() == true
                ? complaint.LinkedMaterials.Select(x => x.MaterialId).ToList()
                : (complaint.MaterialId.HasValue ? new List<int> { complaint.MaterialId.Value } : new List<int>());
            ViewBag.SelectedSupplierIds = complaint.LinkedSuppliers?.Any() == true
                ? complaint.LinkedSuppliers.Select(x => x.SupplierId).ToList()
                : (complaint.SupplierId.HasValue ? new List<int> { complaint.SupplierId.Value } : new List<int>());

            PopulateViewData(complaint.CustomerId);
            return View(complaint);
        }

        // POST: Complaints/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,Title,CustomerId,CustomerName,CustomerContact,CustomerReference,Description,Evaluation,Severity,Investigation,RootCauseCategory,CorrectiveAction,Resolution")] Complaint complaint,
            int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedMaterials, int[]? selectedSuppliers)
        {
            if (id != complaint.Id)
                return NotFound();

            if (User.IsInRole(Constants.ReadOnlyRole))
                return Forbid();

            // Load entity from DB — prevents overposting and preserves all fields not in this form
            var snap     = await _context.Complaint.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            var existing = await _context.Complaint.FindAsync(id);
            if (existing == null) return NotFound();

            // Re-validate status server-side (defence-in-depth)
            if (existing.Status == Complaint.ComplaintStatus.PendingAcceptance ||
                existing.Status == Complaint.ComplaintStatus.Resolution ||
                existing.Status == Complaint.ComplaintStatus.Closed)
                return Forbid();

            // Capture affected-item associations before sync (for the audit diff)
            var oldCpProductIds  = await _context.ComplaintProduct.Where(x => x.ComplaintId == id).Select(x => x.ProductId).ToListAsync();
            var oldCpProcessIds  = await _context.ComplaintProcess.Where(x => x.ComplaintId == id).Select(x => x.ProcessId).ToListAsync();
            var oldCpMaterialIds = await _context.ComplaintMaterial.Where(x => x.ComplaintId == id).Select(x => x.MaterialId).ToListAsync();
            var oldCpSupplierIds = await _context.ComplaintSupplier.Where(x => x.ComplaintId == id).Select(x => x.SupplierId).ToListAsync();

            if (ModelState.IsValid)
            {
                // Apply intake fields — editable at Received and Evaluation stages
                if (existing.Status == Complaint.ComplaintStatus.Received ||
                    existing.Status == Complaint.ComplaintStatus.Evaluation)
                {
                    existing.Title             = complaint.Title;
                    existing.Severity          = complaint.Severity;
                    // Recompute the target resolution date when severity changes so the risk-based
                    // timeframe (measured from receipt) tracks the current severity — mirrors creation.
                    if (snap != null && snap.Severity != complaint.Severity)
                    {
                        var settings = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
                        int resolutionDays = complaint.Severity == Complaint.ComplaintSeverity.Critical
                            ? settings.CriticalComplaintResolutionDays
                            : settings.DefaultComplaintResolutionDays;
                        existing.TargetResolutionDate = existing.ReceivedOn.AddDays(resolutionDays);
                    }
                    existing.CustomerId        = complaint.CustomerId;
                    existing.CustomerContact   = complaint.CustomerContact;
                    existing.CustomerReference = complaint.CustomerReference;
                    existing.Description       = complaint.Description;
                    // Clear old FK columns — junction tables now own the relationship
                    existing.ProductId  = null;
                    existing.ProcessId  = null;
                    existing.MaterialId = null;
                    existing.SupplierId = null;
                    await SyncComplaintJunctions(existing.Id,
                        selectedProducts  ?? Array.Empty<int>(),
                        selectedProcesses ?? Array.Empty<int>(),
                        selectedMaterials ?? Array.Empty<int>(),
                        selectedSuppliers ?? Array.Empty<int>());
                }

                // Apply Evaluation section — only editable at Evaluation status
                if (existing.Status == Complaint.ComplaintStatus.Evaluation)
                {
                    existing.Evaluation        = complaint.Evaluation;
                    existing.Investigation     = complaint.Investigation;
                    existing.RootCauseCategory = complaint.RootCauseCategory;
                    existing.CorrectiveAction  = complaint.CorrectiveAction;
                }

                // Apply Resolution — only editable at Accepted status (locked once submitted for Resolution)
                if (existing.Status == Complaint.ComplaintStatus.Accepted)
                {
                    existing.Resolution = complaint.Resolution;
                }

                try
                {
                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff,     "Title",              snap.Title,           existing.Title);
                        AuditDiffHelper.TrackEnum(diff, "Severity",           snap.Severity,         existing.Severity);
                        AuditDiffHelper.Track(diff,     "Customer Name",      snap.CustomerName,     existing.CustomerName);
                        AuditDiffHelper.Track<int>(diff, "Customer (Id)",     snap.CustomerId,       existing.CustomerId);
                        AuditDiffHelper.Track(diff,     "Customer Contact",   snap.CustomerContact,  existing.CustomerContact);
                        AuditDiffHelper.Track(diff,     "Customer Reference", snap.CustomerReference, existing.CustomerReference);
                        AuditDiffHelper.Track(diff,     "Description",        snap.Description,      existing.Description);
                        AuditDiffHelper.TrackDate(diff, "Target Resolution",  snap.TargetResolutionDate, existing.TargetResolutionDate);
                        AuditDiffHelper.Track(diff,     "Evaluation",         snap.Evaluation,       existing.Evaluation);
                        AuditDiffHelper.Track(diff,     "Investigation",      snap.Investigation,    existing.Investigation);
                        AuditDiffHelper.TrackEnum(diff, "Root Cause Category", snap.RootCauseCategory, existing.RootCauseCategory);
                        AuditDiffHelper.Track(diff,     "Corrective Action",  snap.CorrectiveAction, existing.CorrectiveAction);
                        AuditDiffHelper.Track(diff,     "Resolution",         snap.Resolution,       existing.Resolution);
                    }
                    if (existing.Status == Complaint.ComplaintStatus.Received ||
                        existing.Status == Complaint.ComplaintStatus.Evaluation)
                    {
                        AuditDiffHelper.TrackSet(diff, "Affected Products",  oldCpProductIds,  selectedProducts  ?? Array.Empty<int>(),
                            ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Processes", oldCpProcessIds,  selectedProcesses ?? Array.Empty<int>(),
                            ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Materials", oldCpMaterialIds, selectedMaterials ?? Array.Empty<int>(),
                            ids => _context.Material.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name));
                        AuditDiffHelper.TrackSet(diff, "Affected Suppliers", oldCpSupplierIds, selectedSuppliers ?? Array.Empty<int>(),
                            ids => _context.Supplier.Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id, s => s.LegalName));
                    }
                    if (diff.Count > 0) AddAuditLog(existing.Id, "Edited", null, null, AuditDiffHelper.Build(diff));
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ComplaintExists(existing.Id))
                        return NotFound();
                    throw;
                }

                TempData["Success"] = "Complaint updated successfully.";
                return RedirectToAction(nameof(Details), new { id = existing.Id });
            }

            // Return the DB entity (with applied draft values) so the view has all fields
            if (existing.Status == Complaint.ComplaintStatus.Received ||
                existing.Status == Complaint.ComplaintStatus.Evaluation)
            {
                existing.Title             = complaint.Title;
                existing.Severity          = complaint.Severity;
                existing.CustomerId        = complaint.CustomerId;
                existing.CustomerContact   = complaint.CustomerContact;
                existing.CustomerReference = complaint.CustomerReference;
                existing.Description       = complaint.Description;
            }
            if (existing.Status == Complaint.ComplaintStatus.Evaluation)
            {
                existing.Evaluation        = complaint.Evaluation;
                existing.Investigation     = complaint.Investigation;
                existing.RootCauseCategory = complaint.RootCauseCategory;
                existing.CorrectiveAction  = complaint.CorrectiveAction;
            }
            if (existing.Status == Complaint.ComplaintStatus.Accepted)
                existing.Resolution = complaint.Resolution;

            PopulateViewData(existing.CustomerId);
            ViewBag.SelectedProductIds  = selectedProducts?.ToList()  ?? new List<int>();
            ViewBag.SelectedProcessIds  = selectedProcesses?.ToList() ?? new List<int>();
            ViewBag.SelectedMaterialIds = selectedMaterials?.ToList() ?? new List<int>();
            ViewBag.SelectedSupplierIds = selectedSuppliers?.ToList() ?? new List<int>();
            return View(existing);
        }

        // POST: Workflow — Send for Evaluation (Received → Evaluation)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> Evaluation(int id, string email, string pwd, string? notes)
        {
            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.Status != Complaint.ComplaintStatus.Received)
            {
                TempData["WorkflowError"] = "Complaint must be in Received status to start evaluation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = complaint.Status.ToString();
            complaint.InvestigatedBy = _userManager.GetUserName(User);
            complaint.InvestigatedOn = DateTime.UtcNow;
            complaint.Status = Complaint.ComplaintStatus.Evaluation;

            _context.Update(complaint);
            AddAuditLog(complaint.Id, "Sent for Evaluation", fromStatus, Complaint.ComplaintStatus.Evaluation.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Complaint {complaint.ComplaintId} sent for Evaluation.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Workflow — Submit for Acceptance (Evaluation → PendingAcceptance)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> PendingAcceptance(int id, string email, string pwd, string? notes)
        {
            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.Status != Complaint.ComplaintStatus.Evaluation)
            {
                TempData["WorkflowError"] = "Complaint must be in Evaluation status to submit for acceptance.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(complaint.Evaluation))
            {
                TempData["WorkflowError"] = "Evaluation findings must be recorded before submitting for acceptance.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (complaint.RootCauseCategory == null)
            {
                TempData["WorkflowError"] = "Root Cause Category must be selected before submitting for acceptance.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(complaint.CorrectiveAction))
            {
                TempData["WorkflowError"] = "Corrective Action must be recorded before submitting for acceptance.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = complaint.Status.ToString();
            complaint.ActionBy = _userManager.GetUserName(User);
            complaint.ActionOn = DateTime.UtcNow;
            complaint.Status = Complaint.ComplaintStatus.PendingAcceptance;

            _context.Update(complaint);
            AddAuditLog(complaint.Id, "Submitted for Acceptance", fromStatus, Complaint.ComplaintStatus.PendingAcceptance.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Complaint {complaint.ComplaintId} submitted for Acceptance.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Workflow — Accept (PendingAcceptance → Accepted; Admin/QualityManager/AuthorisedPerson only,
        // matching the Details view and the Deviations/Changes acceptance gate)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Accept(int id, string email, string pwd, string? notes)
        {
            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.Status != Complaint.ComplaintStatus.PendingAcceptance)
            {
                TempData["WorkflowError"] = "Complaint must be in Pending Acceptance status to accept.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = complaint.Status.ToString();
            complaint.AcceptedBy = _userManager.GetUserName(User);
            complaint.AcceptedOn = DateTime.UtcNow;
            complaint.Status = Complaint.ComplaintStatus.Accepted;

            _context.Update(complaint);
            AddAuditLog(complaint.Id, "Accepted", fromStatus, Complaint.ComplaintStatus.Accepted.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Complaint {complaint.ComplaintId} accepted by {complaint.AcceptedBy}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Workflow — Submit for Resolution (Accepted → Resolution; QualityUser+)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> SubmitForResolution(int id, string email, string pwd, string? notes)
        {
            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.Status != Complaint.ComplaintStatus.Accepted)
            {
                TempData["WorkflowError"] = "Complaint must be in Accepted status to submit for resolution.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(complaint.Resolution))
            {
                TempData["WorkflowError"] = "Resolution summary must be documented before submitting for closure.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = complaint.Status.ToString();
            var userName = _userManager.GetUserName(User);
            complaint.ResolvedBy = userName;
            complaint.ResolvedOn = DateTime.UtcNow;
            complaint.Status = Complaint.ComplaintStatus.Resolution;

            _context.Update(complaint);
            AddAuditLog(complaint.Id, "Submitted for Closure", fromStatus, Complaint.ComplaintStatus.Resolution.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Complaint {complaint.ComplaintId} submitted for closure by {userName}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Workflow — Closed (Resolution → Closed; QualityManager/Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Closed(int id, string email, string pwd, string? notes)
        {
            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.Status != Complaint.ComplaintStatus.Resolution)
            {
                TempData["WorkflowError"] = "Complaint must be in Resolution status to close.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(complaint.Resolution))
            {
                TempData["WorkflowError"] = "Resolution summary must be recorded before closing the complaint.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = complaint.Status.ToString();
            complaint.ClosedBy = _userManager.GetUserName(User);
            complaint.ClosedOn = DateTime.UtcNow;
            complaint.Status = Complaint.ComplaintStatus.Closed;
            var _cmpSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            complaint.RetainUntil = complaint.ClosedOn.Value.AddYears(_cmpSets.ComplaintRetentionYears);

            _context.Update(complaint);
            AddAuditLog(complaint.Id, "Closed", fromStatus, Complaint.ComplaintStatus.Closed.ToString(), notes);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Complaint {complaint.ComplaintId} closed by {complaint.ClosedBy}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Workflow — Return for Rework
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> ReturnComplaint(int id, string email, string pwd, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = "A reason is required to return a complaint for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.Status != Complaint.ComplaintStatus.Evaluation
                && complaint.Status != Complaint.ComplaintStatus.PendingAcceptance
                && complaint.Status != Complaint.ComplaintStatus.Accepted
                && complaint.Status != Complaint.ComplaintStatus.Resolution)
            {
                TempData["WorkflowError"] = "Complaint cannot be returned from its current status.";
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

            var fromStatus = complaint.Status;
            Complaint.ComplaintStatus toStatus;

            switch (fromStatus)
            {
                case Complaint.ComplaintStatus.Evaluation:
                    toStatus = Complaint.ComplaintStatus.Received;
                    complaint.InvestigatedBy = null;
                    complaint.InvestigatedOn = null;
                    break;
                case Complaint.ComplaintStatus.PendingAcceptance:
                    toStatus = Complaint.ComplaintStatus.Evaluation;
                    complaint.ActionBy = null;
                    complaint.ActionOn = null;
                    break;
                case Complaint.ComplaintStatus.Accepted:
                    toStatus = Complaint.ComplaintStatus.Evaluation;
                    complaint.AcceptedBy     = null; complaint.AcceptedOn     = null;
                    complaint.ActionBy       = null; complaint.ActionOn       = null;
                    break;
                case Complaint.ComplaintStatus.Resolution:
                    toStatus = Complaint.ComplaintStatus.Accepted;
                    complaint.ResolvedBy = null;
                    complaint.ResolvedOn = null;
                    break;
                default:
                    return Forbid();
            }

            complaint.Status = toStatus;
            _context.Update(complaint);
            AddAuditLog(complaint.Id, $"Returned to {toStatus}", fromStatus.ToString(), toStatus.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Complaint {complaint.ComplaintId} returned to {toStatus} status.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Complaints/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Complaint == null)
                return NotFound();

            var complaint = await _context.Complaint
                .Include(c => c.Product).Include(c => c.Process)
                .Include(c => c.Material).Include(c => c.Supplier).Include(c => c.Customer)
                .Include(c => c.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(c => c.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (complaint == null)
                return NotFound();

            if (complaint.Status == Complaint.ComplaintStatus.PendingAcceptance ||
                complaint.Status == Complaint.ComplaintStatus.Accepted ||
                complaint.Status == Complaint.ComplaintStatus.Resolution ||
                complaint.Status == Complaint.ComplaintStatus.Closed)
                return Forbid();

            return View(complaint);
        }

        // POST: Complaints/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Complaint == null)
                return Problem("Entity set 'ApplicationDbContext.Complaint' is null.");

            var complaint = await _context.Complaint
                .Include(c => c.Attachments)
                .Include(c => c.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (complaint != null)
            {
                if (complaint.Status == Complaint.ComplaintStatus.PendingAcceptance ||
                    complaint.Status == Complaint.ComplaintStatus.Accepted ||
                    complaint.Status == Complaint.ComplaintStatus.Resolution ||
                    complaint.Status == Complaint.ComplaintStatus.Closed)
                    return Forbid();

                // Attachments (rows and files) are retained with the soft-deleted record.
                AddAuditLog(complaint.Id, "Deleted", complaint.Status.ToString(), null, complaint.Title);
                complaint.IsDeleted = true;
                complaint.DeletedBy = _userManager.GetUserName(User);
                complaint.DeletedOn = DateTime.UtcNow;
                _context.Complaint.Update(complaint);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Complaints/ExportRegister
        public async Task<IActionResult> ExportRegister()
        {
            var complaints = await _context.Complaint
                .Include(c => c.Product).Include(c => c.Process)
                .Include(c => c.Material).Include(c => c.Supplier).Include(c => c.Customer)
                .Include(c => c.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(c => c.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(c => c.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .OrderByDescending(c => c.ReceivedOn)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Complaints");

            var headers = new[]
            {
                "Complaint ID", "Title", "Status", "Severity",
                "Product", "Process", "Material", "Customer",
                "Customer Contact", "Customer Reference",
                "Description", "Evaluation",
                "Received By", "Received On",
                "Investigation", "Evaluation Started By", "Evaluation Started On",
                "Root Cause Category", "Corrective Action",
                "Submitted for Acceptance By", "Submitted for Acceptance On",
                "Accepted By", "Accepted On",
                "Resolution", "Closed By", "Closed On"
            };

            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            headerRow.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var c in complaints)
            {
                ws.Cell(row, 1).Value = c.ComplaintId;
                ws.Cell(row, 2).Value = c.Title;
                ws.Cell(row, 3).Value = c.Status.ToString();
                ws.Cell(row, 4).Value = c.Severity.ToString();
                ws.Cell(row, 5).Value = JoinLinked(c.LinkedProducts,  x => x.Product?.Name,       c.Product?.Name);
                ws.Cell(row, 6).Value = JoinLinked(c.LinkedProcesses, x => x.Process?.Name,      c.Process?.Name);
                ws.Cell(row, 7).Value = JoinLinked(c.LinkedMaterials, x => x.Material?.Name,     c.Material?.Name);
                ws.Cell(row, 8).Value = c.Customer?.LegalName ?? "";
                ws.Cell(row, 9).Value = c.CustomerContact ?? "";
                ws.Cell(row, 10).Value = c.CustomerReference ?? "";
                ws.Cell(row, 11).Value = c.Description;
                ws.Cell(row, 12).Value = c.Evaluation ?? "";
                ws.Cell(row, 13).Value = c.ReceivedBy;
                ws.Cell(row, 14).Value = c.ReceivedOn.ToSite().ToShortDateString();
                ws.Cell(row, 15).Value = c.Investigation ?? "";
                ws.Cell(row, 16).Value = c.InvestigatedBy ?? "";
                ws.Cell(row, 17).Value = c.InvestigatedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 18).Value = c.RootCauseCategory?.ToString() ?? "";
                ws.Cell(row, 19).Value = c.CorrectiveAction ?? "";
                ws.Cell(row, 20).Value = c.ActionBy ?? "";
                ws.Cell(row, 21).Value = c.ActionOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 22).Value = c.AcceptedBy ?? "";
                ws.Cell(row, 23).Value = c.AcceptedOn.ToSite()?.ToShortDateString() ?? "";
                ws.Cell(row, 24).Value = c.Resolution ?? "";
                ws.Cell(row, 25).Value = c.ClosedBy ?? "";
                ws.Cell(row, 26).Value = c.ClosedOn.ToSite()?.ToShortDateString() ?? "";

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                row++;
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            OpenQMS.Services.ExcelExportHelper.AddExportInfoSheet(wb, _userManager.GetUserName(User) ?? "Unknown", "Complaints Register", complaints.Count);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            // Register exports are recorded in the workbook's Export Info sheet (who / when / count).
            // No per-record audit entry: there is no parent record, and id 0 violates the audit-log FK.

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Complaints-Register-{SiteTime.Today:yyyy-MM-dd}.xlsx");
        }

        // Attachments
        public async Task<IActionResult> Attachments(int? id)
        {
            if (id == null) return NotFound();
            var complaint = await _context.Complaint
                .Include(c => c.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (complaint == null) return NotFound();
            return View(complaint);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int complaintId, IFormFile file, string? description)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            var complaint = await _context.Complaint.FindAsync(complaintId);
            if (complaint == null) return NotFound();

            if (complaint.Status == Complaint.ComplaintStatus.Closed)
            {
                TempData["Error"] = "Attachments cannot be added to a closed complaint.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds 25 MB limit.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "complaints", complaintId.ToString());
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

            _context.ComplaintAttachment.Add(new ComplaintAttachment
            {
                ComplaintId = complaintId,
                FileName = file.FileName,
                FilePath = $"/uploads/complaints/{complaintId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                Description = description
            });

            AddAuditLog(complaintId, "Attachment uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded successfully.";
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = complaintId });
        }

        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();
            var attachment = await _context.ComplaintAttachment.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ComplaintId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.ComplaintId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ComplaintId });
            }

            AddAuditLog(attachment.ComplaintId, (deleted ? "Deleted attachment downloaded" : "Attachment downloaded"), null, null, attachment.FileName);
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Complaint",
                attachment.ComplaintId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.ComplaintAttachment.FindAsync(id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ComplaintId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.ComplaintId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                TempData["ActiveTab"] = "attachments";
                return RedirectToAction(nameof(Details), new { id = attachment.ComplaintId });
            }

            AddAuditLog(attachment.ComplaintId, "Attachment viewed", null, null, attachment.FileName);
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Complaint",
                attachment.ComplaintId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int complaintId, string? deletionReason)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var attachment = await _context.ComplaintAttachment.FindAsync(id);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                complaintId = attachment.ComplaintId;

                var complaint = await _context.Complaint.FindAsync(complaintId);
                if (complaint != null && complaint.Status == Complaint.ComplaintStatus.Closed)
                {
                    TempData["Error"] = "Attachments cannot be removed from a closed complaint.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = complaintId });
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    TempData["ActiveTab"] = "attachments";
                    return RedirectToAction(nameof(Details), new { id = complaintId });
                }

                // Soft delete: the row and file are retained for traceability.
                attachment.IsDeleted      = true;
                attachment.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                AddAuditLog(complaintId, "Attachment deleted", null, null, $"{attachment.FileName} — Reason: {attachment.DeletionReason}");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attachment deleted successfully.";
            }
            TempData["ActiveTab"] = "attachments";
            return RedirectToAction(nameof(Details), new { id = complaintId });
        }

        // POST: Complaints/LinkAdverseEvent — link an existing adverse event to this complaint
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkAdverseEvent(int complaintId, int adverseEventId)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var complaint = await _context.Complaint.FindAsync(complaintId);
            if (complaint == null) return NotFound();

            if (complaint.Status == Complaint.ComplaintStatus.Closed)
            {
                TempData["Error"] = "Cannot change links on a closed complaint.";
                TempData["ActiveTab"] = "pv";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            var ae = await _context.AdverseEvent.FindAsync(adverseEventId);
            if (ae == null)
            {
                TempData["Error"] = "Selected adverse event not found.";
                TempData["ActiveTab"] = "pv";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            if (ae.LinkedComplaintId.HasValue && ae.LinkedComplaintId != complaintId)
            {
                TempData["Error"] = "That adverse event is already linked to another complaint.";
                TempData["ActiveTab"] = "pv";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            // One-to-one: a complaint may be linked to at most one adverse event.
            var alreadyLinked = complaint.LinkedAEId.HasValue
                || await _context.AdverseEvent.AnyAsync(a => a.LinkedComplaintId == complaintId && a.Id != adverseEventId);
            if (alreadyLinked)
            {
                TempData["Error"] = "This complaint is already linked to an adverse event. Unlink it first.";
                TempData["ActiveTab"] = "pv";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            ae.LinkedComplaintId = complaintId;
            complaint.LinkedAEId = ae.Id;
            AddAuditLog(complaintId, "Adverse Event linked", null, null, ae.AENumber);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Linked adverse event {ae.AENumber}.";
            TempData["ActiveTab"] = "pv";
            return RedirectToAction(nameof(Details), new { id = complaintId });
        }

        // POST: Complaints/UnlinkAdverseEvent — remove an adverse event link from this complaint
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlinkAdverseEvent(int complaintId, int adverseEventId)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();

            var complaint = await _context.Complaint.FindAsync(complaintId);
            if (complaint == null) return NotFound();

            if (complaint.Status == Complaint.ComplaintStatus.Closed)
            {
                TempData["Error"] = "Cannot change links on a closed complaint.";
                TempData["ActiveTab"] = "pv";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            var ae = await _context.AdverseEvent.FindAsync(adverseEventId);
            if (ae != null && ae.LinkedComplaintId == complaintId)
            {
                ae.LinkedComplaintId = null;
                AddAuditLog(complaintId, "Adverse Event unlinked", null, null, ae.AENumber);
            }

            if (complaint.LinkedAEId == adverseEventId)
            {
                complaint.LinkedAEId = await _context.AdverseEvent
                    .Where(a => a.LinkedComplaintId == complaintId && a.Id != adverseEventId)
                    .Select(a => (int?)a.Id)
                    .FirstOrDefaultAsync();
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Adverse event unlinked.";
            TempData["ActiveTab"] = "pv";
            return RedirectToAction(nameof(Details), new { id = complaintId });
        }

        // POST: Create CAPA from Complaint
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCapa(int? id)
        {
            if (id == null) return NotFound();
            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.LinkedCapaId.HasValue)
            {
                TempData["Error"] = "A CAPA is already linked to this complaint.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return RedirectToAction("Create", "Capas", new { complaintId = id });
        }

        public async Task<IActionResult> ExportComplaintDetail(int id)
        {
            if (_context.Complaint == null) return NotFound();
            var complaint = await _context.Complaint
                .Where(x => x.Id == id)
                .Include(x => x.Customer).Include(x => x.Product)
                .Include(x => x.Process).Include(x => x.Material).Include(x => x.Supplier)
                .Include(x => x.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(x => x.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync();
            if (complaint == null) return NotFound();

            var fields = new List<(string, string?)>
            {
                ("Complaint ID",                complaint.ComplaintId),
                ("Title",                       complaint.Title),
                ("Status",                      complaint.Status.ToString()),
                ("Severity",                    complaint.Severity.ToString()),
                ("Customer",                    complaint.Customer?.LegalName),
                ("Contact",                     complaint.CustomerContact),
                ("Reference",                   complaint.CustomerReference),
                ("Received On",                 complaint.ReceivedOn.ToSite().ToShortDateString()),
                ("Received By",                 complaint.ReceivedBy),
                ("Product",                     JoinLinked(complaint.LinkedProducts,  x => x.Product?.Name,    complaint.Product?.Name)),
                ("Process",                     JoinLinked(complaint.LinkedProcesses, x => x.Process?.Name,   complaint.Process?.Name)),
                ("Material",                    JoinLinked(complaint.LinkedMaterials, x => x.Material?.Name,  complaint.Material?.Name)),
                ("Supplier",                    JoinLinked(complaint.LinkedSuppliers, x => x.Supplier?.LegalName, complaint.Supplier?.LegalName)),
                ("Description",                 complaint.Description),
                ("Evaluation",                  complaint.Evaluation),
                ("Investigation",               complaint.Investigation),
                ("Root Cause Category",         complaint.RootCauseCategory?.ToString()),
                ("Corrective Action",           complaint.CorrectiveAction),
                ("Evaluation Started By",       complaint.InvestigatedBy),
                ("Evaluation Started On",       complaint.InvestigatedOn.ToSite()?.ToShortDateString()),
                ("Submitted for Acceptance By", complaint.ActionBy),
                ("Submitted for Acceptance On", complaint.ActionOn.ToSite()?.ToShortDateString()),
                ("Accepted By",                 complaint.AcceptedBy),
                ("Accepted On",                 complaint.AcceptedOn.ToSite()?.ToShortDateString()),
                ("Resolution",                  complaint.Resolution),
                ("Closed By",                   complaint.ClosedBy),
                ("Closed On",                   complaint.ClosedOn.ToSite()?.ToShortDateString()),
            };

            AddAuditLog(complaint.Id, "Exported (PDF)", complaint.Status.ToString(), complaint.Status.ToString());
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Complaint Detail Report", complaint.ComplaintId, complaint.Status.ToString(), fields,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Complaint-{complaint.ComplaintId}.pdf");
        }

        public async Task<IActionResult> ExportDetailInCSV(int id)
        {
            if (_context.Complaint == null) return NotFound();
            var complaint = await _context.Complaint
                .Where(x => x.Id == id)
                .Include(x => x.Customer).Include(x => x.Product)
                .Include(x => x.Process).Include(x => x.Material).Include(x => x.Supplier)
                .Include(x => x.LinkedProducts!).ThenInclude(x => x.Product)
                .Include(x => x.LinkedProcesses!).ThenInclude(x => x.Process)
                .Include(x => x.LinkedMaterials!).ThenInclude(x => x.Material)
                .Include(x => x.LinkedSuppliers!).ThenInclude(x => x.Supplier)
                .FirstOrDefaultAsync();
            if (complaint == null) return NotFound();

            AddAuditLog(complaint.Id, "Exported (CSV)", complaint.Status.ToString(), complaint.Status.ToString());
            await _context.SaveChangesAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Complaint ID,Title,Status,Severity,Customer,Received On,Received By,Product,Process,Material,Supplier,Description,Evaluation,Root Cause Category,Corrective Action,Accepted By,Accepted On,Resolution,Closed By,Closed On");
            builder.AppendLine($"{CsvSafe(complaint.ComplaintId)},{CsvSafe(complaint.Title)},{complaint.Status},{complaint.Severity},{CsvSafe(complaint.Customer?.LegalName)},{complaint.ReceivedOn.ToSite().ToShortDateString()},{CsvSafe(complaint.ReceivedBy)},{CsvSafe(JoinLinked(complaint.LinkedProducts, x => x.Product?.Name, complaint.Product?.Name))},{CsvSafe(JoinLinked(complaint.LinkedProcesses, x => x.Process?.Name, complaint.Process?.Name))},{CsvSafe(JoinLinked(complaint.LinkedMaterials, x => x.Material?.Name, complaint.Material?.Name))},{CsvSafe(JoinLinked(complaint.LinkedSuppliers, x => x.Supplier?.LegalName, complaint.Supplier?.LegalName))},{CsvSafe(complaint.Description)},{CsvSafe(complaint.Evaluation)},{CsvSafe(complaint.RootCauseCategory?.ToString())},{CsvSafe(complaint.CorrectiveAction)},{CsvSafe(complaint.AcceptedBy)},{complaint.AcceptedOn.ToSite()?.ToShortDateString() ?? ""},{CsvSafe(complaint.Resolution)},{CsvSafe(complaint.ClosedBy)},{complaint.ClosedOn.ToSite()?.ToShortDateString() ?? ""}");

            return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"Complaint-{complaint.ComplaintId}.csv");
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

        private bool ComplaintExists(int id)
        {
            return (_context.Complaint?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AdminReopen(int id, string reason, string email, string pwd)
        {
            var complaint = await _context.Complaint.FindAsync(id);
            if (complaint == null) return NotFound();

            if (complaint.Status != Complaint.ComplaintStatus.Closed)
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

            var fromStatus = complaint.Status.ToString();
            complaint.Status = Complaint.ComplaintStatus.Resolution;
            AddAuditLog(complaint.Id, "Administrative Reopen", fromStatus, Complaint.ComplaintStatus.Resolution.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Record has been administratively reopened to Resolution stage.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
