using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.LC;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public SuppliersController(
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
            ViewBag.Products  = new SelectList(_context.Product.Where(p => !p.IsDeleted && p.Status == Product.ProductStatus.Approved).OrderBy(p => p.Name), "Id", "Name");
            ViewBag.Materials = new SelectList(_context.Material.Where(m => !m.IsDeleted && m.Status == Material.MaterialStatus.Approved).OrderBy(m => m.Name), "Id", "Name");
            ViewBag.Services  = new SelectList(_context.Service.Where(s => !s.IsDeleted && s.Status == Service.ServiceStatus.Approved).OrderBy(s => s.Name), "Id", "Name");
        }

        private async Task AddAuditLog(int supplierId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.SupplierAuditLog.Add(new SupplierAuditLog
            {
                ParentId = supplierId,
                Action = action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                PerformedBy = User.Identity?.Name ?? "Unknown",
                PerformedOn = DateTime.UtcNow,
                Notes = notes
            });
            await _context.SaveChangesAsync();
        }

        // Server-side workflow guard: a transition is only legal from the states in which the
        // Details page offers it, so a crafted request cannot skip or reorder workflow steps.
        private bool TransitionAllowed(Supplier supplier, string action, out IActionResult rejection, params SupplierStatus[] allowedFrom)
        {
            if (allowedFrom.Contains(supplier.Status)) { rejection = null!; return true; }
            TempData["WorkflowError"] = $"Cannot {action}: the supplier is {EnumHelper.Display(supplier.Status)}.";
            rejection = RedirectToAction(nameof(Details), new { id = supplier.Id });
            return false;
        }

        // Supplier documents are locked once a workflow reaches a final outcome (matches the
        // Documents page); a new review (or reactivation) returns the supplier to Pending and reopens them.
        private static bool DocumentsLocked(SupplierStatus status) =>
            status is SupplierStatus.Approved or SupplierStatus.Qualified or SupplierStatus.Suspended
                   or SupplierStatus.Cancelled or SupplierStatus.Rejected;

        // Approval expiry (optional): when set it must lie in the future and carry a documented
        // reason (conditional / time-limited approval). Returns an error message, or null if valid.
        private static string? ValidateApprovalExpiry(DateTime? expiry, string? reason)
        {
            if (!expiry.HasValue) return null;
            if (expiry.Value.Date <= SiteTime.Today)
                return "Approval expiry must be a future date (or left blank for no fixed expiry).";
            if (string.IsNullOrWhiteSpace(reason))
                return "A reason is required for a time-limited approval (e.g. the condition or why the approval is time-limited).";
            return null;
        }

        private static bool HasFutureReviewDate(Supplier s) =>
            s.NextReviewDate.HasValue && s.NextReviewDate.Value.Date > SiteTime.Today;

        private static string? WithExpiryNote(string? notes, DateTime? expiry, string? reason)
        {
            if (!expiry.HasValue) return notes;
            var e = $"Approval expires {expiry.Value:dd MMM yyyy} — Reason: {reason}";
            return string.IsNullOrWhiteSpace(notes) ? e : $"{notes} | {e}";
        }

        private async Task<bool> VerifyCredentials(string email, string pwd)
        {
            // E-signature must be made with the signed-in user's own credentials
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user = null;
            if (user == null) return false;
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, pwd);
            return result != PasswordVerificationResult.Failed;
        }

        private string GenerateSupplierId()
        {
            var allIds = _context.Supplier.AsEnumerable().Select(x =>
            {
                if (!string.IsNullOrEmpty(x.SupplierCode))
                {
                    var parts = x.SupplierCode.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var n)) return n;
                }
                return 0;
            });
            var next = (allIds.Any() ? allIds.Max() : 0) + 1;
            return $"SUP-{next.ToString().PadLeft(4, '0')}";
        }

        // ============================================================
        // DASHBOARD
        // ============================================================

        public async Task<IActionResult> Dashboard()
        {
            var all = await _context.Supplier.ToListAsync();

            var approved = all.Where(s => s.ApprovalStatus == SupplierStatus.Approved).ToList();
            var qualified = all.Where(s => s.ApprovalStatus == SupplierStatus.Qualified).ToList();
            var suspended = all.Where(s => s.ApprovalStatus == SupplierStatus.Suspended).ToList();
            var cancelled = all.Where(s => s.ApprovalStatus == SupplierStatus.Cancelled).ToList();
            var pending = all.Where(s => s.ApprovalStatus == null || s.ApprovalStatus == SupplierStatus.Pending).ToList();

            var overdueReview = all.Where(s =>
                (s.ApprovalStatus == SupplierStatus.Approved || s.ApprovalStatus == SupplierStatus.Qualified)
                && s.NextReviewDate.HasValue && s.NextReviewDate.Value < SiteTime.Today).ToList();

            var approachingReview = all.Where(s =>
                (s.ApprovalStatus == SupplierStatus.Approved || s.ApprovalStatus == SupplierStatus.Qualified)
                && s.NextReviewDate.HasValue
                && s.NextReviewDate.Value >= SiteTime.Today
                && s.NextReviewDate.Value <= SiteTime.Today.AddDays(30)).ToList();

            var pendingApproval = all.Where(s =>
                s.Status == SupplierStatus.UnderAssessment ||
                s.Status == SupplierStatus.PendingSuspension ||
                s.Status == SupplierStatus.PendingCancellation).ToList();

            // Renewals / revisions: supplier keeps its standing approval while the new workflow is open
            var reReviews = all.Where(s =>
                (s.ApprovalStatus == SupplierStatus.Approved || s.ApprovalStatus == SupplierStatus.Qualified)
                && (s.Status == SupplierStatus.Pending || s.Status == SupplierStatus.UnderAssessment)).ToList();
            var reReviewIds = reReviews.Select(s => s.Id).ToList();
            ViewBag.ReReviews = reReviews;
            ViewBag.ReReviewStarted = await _context.SupplierReview
                .Where(r => reReviewIds.Contains(r.SupplierId))
                .GroupBy(r => r.SupplierId)
                .Select(g => new { SupplierId = g.Key, Started = g.Max(r => r.ReviewDate) })
                .ToDictionaryAsync(x => x.SupplierId, x => x.Started);

            var statusCounts = new Dictionary<string, int>
            {
                ["Pending"] = pending.Count,
                ["Approved"] = approved.Count,
                ["Qualified"] = qualified.Count,
                ["Suspended"] = suspended.Count,
                ["Cancelled"] = cancelled.Count
            };

            var typeCounts = new Dictionary<string, int>
            {
                ["Product"] = all.Count(s => s.SupplierType == SupplierType.Product),
                ["Material"] = all.Count(s => s.SupplierType == SupplierType.Material),
                ["Service"] = all.Count(s => s.SupplierType == SupplierType.Service)
            };

            var riskCounts = new Dictionary<string, int>
            {
                ["Critical"] = all.Count(s => s.ApprovalStatus != null && s.RiskLevel == SupplierRiskLevel.Critical),
                ["High"] = all.Count(s => s.ApprovalStatus != null && s.RiskLevel == SupplierRiskLevel.High),
                ["Medium"] = all.Count(s => s.ApprovalStatus != null && s.RiskLevel == SupplierRiskLevel.Medium),
                ["Low"] = all.Count(s => s.ApprovalStatus != null && s.RiskLevel == SupplierRiskLevel.Low),
                ["NotAssessed"] = all.Count(s => s.ApprovalStatus != null && s.RiskLevel == SupplierRiskLevel.NotAssessed)
            };

            var expiredSuppliers = all.Where(s =>
                (s.ApprovalStatus == SupplierStatus.Approved || s.ApprovalStatus == SupplierStatus.Qualified)
                && s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date < SiteTime.Today).ToList();

            var expiringSuppliers = all.Where(s =>
                (s.ApprovalStatus == SupplierStatus.Approved || s.ApprovalStatus == SupplierStatus.Qualified)
                && s.ExpiryDate.HasValue
                && s.ExpiryDate.Value.Date >= SiteTime.Today
                && s.ExpiryDate.Value.Date <= SiteTime.Today.AddDays(30)).ToList();

            var recentActivity = await _context.SupplierAuditLog
                .OrderByDescending(l => l.PerformedOn)
                .Take(12)
                .ToListAsync();

            var recentIds = recentActivity.Select(l => l.ParentId).Distinct().ToList();
            var recentSuppliers = await _context.Supplier
                .Where(s => recentIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => new { s.SupplierCode, s.LegalName });

            ViewBag.Total = all.Count;
            ViewBag.ApprovedCount = approved.Count;
            ViewBag.QualifiedCount = qualified.Count;
            ViewBag.SuspendedCount = suspended.Count;
            ViewBag.CancelledCount = cancelled.Count;
            ViewBag.PendingCount = pending.Count;
            ViewBag.OverdueReviewCount = overdueReview.Count;
            ViewBag.StatusCounts = statusCounts;
            ViewBag.TypeCounts = typeCounts;
            ViewBag.RiskCounts = riskCounts;
            ViewBag.OverdueReview = overdueReview;
            ViewBag.ApproachingReview = approachingReview;
            ViewBag.PendingApproval = pendingApproval;
            ViewBag.RecentActivity = recentActivity;
            ViewBag.RecentSuppliers = recentSuppliers;
            ViewBag.ExpiredCount = expiredSuppliers.Count;
            ViewBag.ExpiringCount = expiringSuppliers.Count;
            ViewBag.ExpiredSuppliers = expiredSuppliers;
            ViewBag.ExpiringSuppliers = expiringSuppliers;

            return View();
        }

        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index()
        {
            var suppliers = await _context.Supplier
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.SupplierCode)
                .ToListAsync();
            return View(suppliers);
        }

        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Supplier
                .Include(s => s.Addresses)
                .Include(s => s.Documents)
                .Include(s => s.Reviews)
                .Include(s => s.QualityRecords)
                .Include(s => s.AuditLogs)
                .Include(s => s.SupplierMaterials).ThenInclude(sm => sm.Material)
                .Include(s => s.SupplierProducts).ThenInclude(sp => sp.Product)
                .Include(s => s.SupplierServices).ThenInclude(ss => ss.Service)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (supplier == null) return NotFound();

            ViewBag.LCRecords = await _context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted && r.SupplierId == id)
                .Include(r => r.LicenceType)
                .OrderBy(r => r.LCNumber)
                .ToListAsync();

            return View(supplier);
        }

        // ============================================================
        // CREATE
        // ============================================================

        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public IActionResult Create()
        {
            PopulateViewData();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> Create(
            [Bind("LegalName,TradingName,LegalId,Website,Street,City,State,PostCode,Country,ContactName,ContactEmail,ContactPhone,SupplierType,QualificationRequired,ReviewFrequency")] Supplier supplier,
            int[]? selectedProducts,
            int[]? selectedMaterials,
            int[]? selectedServices)
        {
            if (ModelState.IsValid)
            {
                var duplicate = await _context.Supplier
                    .Where(s => !s.IsDeleted && s.LegalName.ToLower() == supplier.LegalName.ToLower())
                    .FirstOrDefaultAsync();
                if (duplicate != null)
                    TempData["Warning"] = $"A supplier with the legal name '{supplier.LegalName}' already exists ({duplicate.SupplierCode}). Verify this is a different entity.";

                supplier.SupplierCode = GenerateSupplierId();
                supplier.Status = SupplierStatus.Pending;
                supplier.TestingRequirement = TestingRequirement.FullTesting;
                supplier.CreatedBy = _userManager.GetUserName(User) ?? "Unknown";
                supplier.CreatedOn = DateTime.UtcNow;

                _context.Add(supplier);
                await _context.SaveChangesAsync();

                await SaveJunctionRecords(supplier.Id, supplier.SupplierType, selectedProducts, selectedMaterials, selectedServices);
                await AddAuditLog(supplier.Id, "Created", null, "Pending",
                    $"ID: {supplier.SupplierCode}; Supplier: {supplier.LegalName}");

                return RedirectToAction(nameof(Details), new { id = supplier.Id });
            }
            PopulateViewData();
            return View(supplier);
        }

        // ============================================================
        // EDIT
        // ============================================================

        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Supplier
                .Include(s => s.SupplierMaterials)
                .Include(s => s.SupplierProducts)
                .Include(s => s.SupplierServices)
                .Include(s => s.Reviews)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();

            if (supplier.Status != SupplierStatus.Pending)
                return RedirectToAction(nameof(Details), new { id });

            PopulateViewData();

            ViewBag.SelectedProducts = supplier.SupplierProducts?.Select(sp => sp.ProductId).ToList() ?? new List<int>();
            ViewBag.SelectedMaterials = supplier.SupplierMaterials?.Select(sm => sm.MaterialId).ToList() ?? new List<int>();
            ViewBag.SelectedServices = supplier.SupplierServices?.Select(ss => ss.ServiceId).ToList() ?? new List<int>();

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,SupplierCode,LegalName,TradingName,LegalId,Website,Street,City,State,PostCode,Country,ContactName,ContactEmail,ContactPhone,SupplierType,QualificationRequired,ReviewFrequency,NextReviewDate,RiskLevel,RiskJustification,ApprovalComments,QualificationJustification")] Supplier supplier,
            int[]? selectedProducts,
            int[]? selectedMaterials,
            int[]? selectedServices)
        {
            if (id != supplier.Id) return NotFound();

            // Load the stored record: workflow and system fields (status, standing approval,
            // review trigger/dates, sign-offs, testing requirement, code, created-by) are
            // never taken from the form — only the editable fields below are copied across.
            var existing = await _context.Supplier.FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null) return NotFound();

            if (existing.Status != SupplierStatus.Pending)
                return RedirectToAction(nameof(Details), new { id });

            // Non-form fields the view needs if the form is redisplayed after a validation error
            supplier.SupplierCode   = existing.SupplierCode;
            supplier.Status         = existing.Status;
            supplier.ApprovalStatus = existing.ApprovalStatus;
            supplier.ReviewTrigger  = existing.ReviewTrigger;
            supplier.LastReviewDate = existing.LastReviewDate;
            supplier.CreatedBy      = existing.CreatedBy;
            supplier.CreatedOn      = existing.CreatedOn;
            supplier.Reviews        = await _context.SupplierReview.Where(r => r.SupplierId == id).ToListAsync();

            if (ModelState.IsValid)
            {
                try
                {
                    var snap = await _context.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                    // Capture linked-record associations before the sync below (for the audit diff)
                    var oldSpProductIds  = await _context.SupplierProduct.Where(x => x.SupplierId == id).Select(x => x.ProductId).ToListAsync();
                    var oldSpMaterialIds = await _context.SupplierMaterial.Where(x => x.SupplierId == id).Select(x => x.MaterialId).ToListAsync();
                    var oldSpServiceIds  = await _context.SupplierService.Where(x => x.SupplierId == id).Select(x => x.ServiceId).ToListAsync();

                    existing.LegalName         = supplier.LegalName;
                    existing.TradingName       = supplier.TradingName;
                    existing.LegalId           = supplier.LegalId;
                    existing.Website           = supplier.Website;
                    existing.Street            = supplier.Street;
                    existing.City              = supplier.City;
                    existing.State             = supplier.State;
                    existing.PostCode          = supplier.PostCode;
                    existing.Country           = supplier.Country;
                    existing.ContactName       = supplier.ContactName;
                    existing.ContactEmail      = supplier.ContactEmail;
                    existing.ContactPhone      = supplier.ContactPhone;
                    existing.SupplierType      = supplier.SupplierType;
                    existing.RiskLevel         = supplier.RiskLevel;
                    existing.RiskJustification = supplier.RiskJustification;
                    existing.ReviewFrequency   = supplier.ReviewFrequency;
                    existing.NextReviewDate    = supplier.NextReviewDate;
                    existing.ApprovalComments  = supplier.ApprovalComments;
                    // The form only shows these for suppliers holding a standing approval
                    if (existing.ApprovalStatus != null)
                    {
                        existing.QualificationRequired      = supplier.QualificationRequired;
                        existing.QualificationJustification = supplier.QualificationJustification;
                    }

                    await _context.SaveChangesAsync();
                    await SyncJunctionRecords(existing.Id, existing.SupplierType, selectedProducts, selectedMaterials, selectedServices);

                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff,     "Legal Name",            snap.LegalName,           existing.LegalName);
                        AuditDiffHelper.Track(diff,     "Trading Name",          snap.TradingName,         existing.TradingName);
                        AuditDiffHelper.Track(diff,     "Legal ID",              snap.LegalId,             existing.LegalId);
                        AuditDiffHelper.Track(diff,     "Website",               snap.Website,             existing.Website);
                        AuditDiffHelper.Track(diff,     "Contact Name",          snap.ContactName,         existing.ContactName);
                        AuditDiffHelper.Track(diff,     "Contact Email",         snap.ContactEmail,        existing.ContactEmail);
                        AuditDiffHelper.Track(diff,     "Contact Phone",         snap.ContactPhone,        existing.ContactPhone);
                        AuditDiffHelper.Track(diff,     "Address",               $"{snap.Street}, {snap.City} {snap.State} {snap.PostCode}, {snap.Country}", $"{existing.Street}, {existing.City} {existing.State} {existing.PostCode}, {existing.Country}");
                        AuditDiffHelper.TrackEnum(diff, "Supplier Type",         snap.SupplierType,         existing.SupplierType);
                        AuditDiffHelper.Track<bool>(diff, "Qualification Required", snap.QualificationRequired, existing.QualificationRequired);
                        AuditDiffHelper.TrackEnum(diff, "Testing Requirement",   snap.TestingRequirement,   existing.TestingRequirement);
                        AuditDiffHelper.TrackEnum(diff, "Risk Level",            snap.RiskLevel,            existing.RiskLevel);
                        AuditDiffHelper.Track(diff,     "Risk Justification",    snap.RiskJustification,   existing.RiskJustification);
                        AuditDiffHelper.TrackEnum(diff, "Review Frequency",      snap.ReviewFrequency,      existing.ReviewFrequency);
                        AuditDiffHelper.TrackDate(diff, "Next Review Date",      snap.NextReviewDate,       existing.NextReviewDate);
                        AuditDiffHelper.TrackDate(diff, "Approval Expiry",       snap.ExpiryDate,           existing.ExpiryDate);
                        AuditDiffHelper.Track(diff,     "Qualification Notes",   snap.QualificationJustification, existing.QualificationJustification);
                        AuditDiffHelper.Track(diff,     "Approval Comments",     snap.ApprovalComments,    existing.ApprovalComments);
                    }
                    // Linked records are type-gated (mirror SaveJunctionRecords): only the relevant set is editable.
                    if (existing.SupplierType == SupplierType.Product)
                        AuditDiffHelper.TrackSet(diff, "Linked Products",  oldSpProductIds,  selectedProducts  ?? Array.Empty<int>(),
                            ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                    else if (existing.SupplierType == SupplierType.Material)
                        AuditDiffHelper.TrackSet(diff, "Linked Materials", oldSpMaterialIds, selectedMaterials ?? Array.Empty<int>(),
                            ids => _context.Material.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name));
                    else if (existing.SupplierType == SupplierType.Service)
                        AuditDiffHelper.TrackSet(diff, "Linked Services",  oldSpServiceIds,  selectedServices  ?? Array.Empty<int>(),
                            ids => _context.Service.Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name));
                    if (diff.Count > 0) await AddAuditLog(existing.Id, "Edited", null, null, AuditDiffHelper.Build(diff));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Details), new { id });
            }
            PopulateViewData();
            return View(supplier);
        }

        // ============================================================
        // DELETE
        // ============================================================

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Supplier.FirstOrDefaultAsync(m => m.Id == id);
            if (supplier == null) return NotFound();

            if (supplier.Status != SupplierStatus.Pending && supplier.Status != SupplierStatus.UnderAssessment)
            {
                TempData["Error"] = "Only suppliers in Pending or Under Assessment status can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Supplier.FindAsync(id);
            if (supplier != null)
            {
                if (supplier.Status != SupplierStatus.Pending && supplier.Status != SupplierStatus.UnderAssessment)
                {
                    TempData["Error"] = "Only suppliers in Pending or Under Assessment status can be deleted.";
                    return RedirectToAction(nameof(Index));
                }

                var fromStatus = supplier.Status.ToString();
                supplier.IsDeleted = true;
                supplier.DeletedBy = _userManager.GetUserName(User);
                supplier.DeletedOn = DateTime.UtcNow;
                _context.Supplier.Update(supplier);
                await _context.SaveChangesAsync();
                await AddAuditLog(supplier.Id, "Deleted", fromStatus, null, supplier.LegalName);
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // EXPORT
        // ============================================================

        public async Task<IActionResult> ExportRegister()
        {
            var suppliers = await _context.Supplier
                .Include(s => s.SupplierProducts).ThenInclude(sp => sp.Product)
                .Include(s => s.SupplierMaterials).ThenInclude(sm => sm.Material)
                .Include(s => s.SupplierServices).ThenInclude(ss => ss.Service)
                .OrderByDescending(s => s.SupplierCode)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Supplier Register");

            var headers = new[] {
                "Supplier ID","Legal Name","Trading Name","Type","Risk Level","Status","Workflow Status",
                "Assessed By","Assessed On","Approved By","Approved On","Qualified By","Qualified On",
                "Review Frequency","Last Review","Next Review","Effective Date","Approval Expiry",
                "Qualification Required","Country","Created By","Created On"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");
                cell.Style.Font.FontColor = XLColor.White;
            }

            for (int r = 0; r < suppliers.Count; r++)
            {
                var s = suppliers[r];
                var row = r + 2;
                string linkedItems = string.Join(", ",
                    s.SupplierProducts?.Select(x => x.Product?.Name ?? "") ?? Enumerable.Empty<string>(),
                    s.SupplierMaterials?.Select(x => x.Material?.Name ?? "") ?? Enumerable.Empty<string>(),
                    s.SupplierServices?.Select(x => x.Service?.Name ?? "") ?? Enumerable.Empty<string>()
                ).Trim(',', ' ');

                ws.Cell(row, 1).Value = s.SupplierCode;
                ws.Cell(row, 2).Value = s.LegalName;
                ws.Cell(row, 3).Value = s.TradingName ?? "";
                ws.Cell(row, 4).Value = s.SupplierType.ToString();
                ws.Cell(row, 5).Value = s.ApprovalStatus != null && s.RiskLevel != SupplierRiskLevel.NotAssessed ? s.RiskLevel.ToString() : "-";
                ws.Cell(row, 6).Value = s.ApprovalStatus?.ToString() ?? "Pending";
                ws.Cell(row, 7).Value = s.Status.ToString();
                ws.Cell(row, 8).Value = s.AssessedBy ?? "";
                ws.Cell(row, 9).Value = s.AssessedOn.HasValue ? s.AssessedOn.Value.ToSite().ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 10).Value = s.ApprovedBy ?? "";
                ws.Cell(row, 11).Value = s.ApprovedOn.HasValue ? s.ApprovedOn.Value.ToSite().ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 12).Value = s.QualifiedBy ?? "";
                ws.Cell(row, 13).Value = s.QualifiedOn.HasValue ? s.QualifiedOn.Value.ToSite().ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 14).Value = s.ReviewFrequency.ToString();
                ws.Cell(row, 15).Value = s.LastReviewDate.HasValue ? s.LastReviewDate.Value.ToSite().ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 16).Value = s.NextReviewDate.HasValue ? s.NextReviewDate.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 17).Value = s.EffectiveDate.HasValue ? s.EffectiveDate.Value.ToSite().ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 18).Value = s.ExpiryDate.HasValue ? s.ExpiryDate.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 19).Value = s.QualificationRequired ? "Yes" : "No";
                ws.Cell(row, 20).Value = s.Country ?? "";
                ws.Cell(row, 21).Value = s.CreatedBy ?? "";
                ws.Cell(row, 22).Value = s.CreatedOn.ToSite().ToString("dd/MM/yyyy");

                if (r % 2 == 1)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Supplier_Register_{SiteTime.Today:yyyyMMdd}.xlsx");
        }

        // ============================================================
        // JUNCTION RECORD HELPERS
        // ============================================================

        private async Task SaveJunctionRecords(int supplierId, SupplierType type, int[]? products, int[]? materials, int[]? services)
        {
            if (type == SupplierType.Product && products != null)
            {
                foreach (var pid in products)
                    _context.SupplierProduct.Add(new SupplierProduct { SupplierId = supplierId, ProductId = pid });
            }
            else if (type == SupplierType.Material && materials != null)
            {
                foreach (var mid in materials)
                    _context.SupplierMaterial.Add(new SupplierMaterial { SupplierId = supplierId, MaterialId = mid });
            }
            else if (type == SupplierType.Service && services != null)
            {
                foreach (var sid in services)
                    _context.SupplierService.Add(new SupplierService { SupplierId = supplierId, ServiceId = sid });
            }
            await _context.SaveChangesAsync();
        }

        private async Task SyncJunctionRecords(int supplierId, SupplierType type, int[]? products, int[]? materials, int[]? services)
        {
            var existingProducts = _context.SupplierProduct.Where(sp => sp.SupplierId == supplierId);
            var existingMaterials = _context.SupplierMaterial.Where(sm => sm.SupplierId == supplierId);
            var existingServices = _context.SupplierService.Where(ss => ss.SupplierId == supplierId);
            _context.SupplierProduct.RemoveRange(existingProducts);
            _context.SupplierMaterial.RemoveRange(existingMaterials);
            _context.SupplierService.RemoveRange(existingServices);
            await _context.SaveChangesAsync();

            await SaveJunctionRecords(supplierId, type, products, materials, services);
        }

        // ============================================================
        // WORKFLOW ACTIONS
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> StartAssessment(int? id, string email, string pwd,
            string? suspensionReason = null, string? cancellationReason = null)
        {
            if (id == null) return NotFound();
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var supplier = await _context.Supplier.FindAsync(id);
            if (supplier != null)
            {
                if (!TransitionAllowed(supplier, "submit the assessment", out var rejected, SupplierStatus.Pending)) return rejected;

                // "Recommend Suspension" is only offered for suppliers that have held an approval
                if (!string.IsNullOrWhiteSpace(suspensionReason)
                    && (supplier.ApprovalStatus == null || supplier.ApprovalStatus == SupplierStatus.Pending))
                {
                    TempData["WorkflowError"] = "Suspension can only be recommended for a supplier that has previously been approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                var fromStatus = supplier.Status.ToString();
                supplier.AssessedBy = email;
                supplier.AssessedOn = DateTime.UtcNow;

                string toStatus;
                if (!string.IsNullOrWhiteSpace(cancellationReason))
                {
                    supplier.CancellationReason = cancellationReason;
                    supplier.SuspensionReason = null;
                    supplier.Status = SupplierStatus.PendingCancellation;
                    toStatus = "PendingCancellation";
                }
                else if (!string.IsNullOrWhiteSpace(suspensionReason))
                {
                    supplier.SuspensionReason = suspensionReason;
                    supplier.CancellationReason = null;
                    supplier.Status = SupplierStatus.PendingSuspension;
                    toStatus = "PendingSuspension";
                }
                else
                {
                    supplier.SuspensionReason = null;
                    supplier.CancellationReason = null;
                    supplier.Status = SupplierStatus.UnderAssessment;
                    toStatus = "UnderAssessment";
                }
                await _context.SaveChangesAsync();
                await AddAuditLog(supplier.Id, "Assessment Submitted", fromStatus, toStatus);
                TempData["Success"] = "Assessment submitted successfully.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> ReturnToAssessment(int? id, string email, string pwd, string? notes = null)
        {
            if (id == null) return NotFound();
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var supplier = await _context.Supplier.FindAsync(id);
            if (supplier != null)
            {
                if (!TransitionAllowed(supplier, "return to assessment", out var rejected, SupplierStatus.UnderAssessment, SupplierStatus.PendingSuspension, SupplierStatus.PendingCancellation)) return rejected;
                var fromStatus = supplier.Status.ToString();
                supplier.Status = SupplierStatus.Pending;
                supplier.AssessedBy = null;
                supplier.AssessedOn = null;
                supplier.SuspensionReason = null;
                supplier.CancellationReason = null;
                await _context.SaveChangesAsync();
                await AddAuditLog(supplier.Id, "Returned to Assessment", fromStatus, "Pending", notes);
                TempData["Success"] = "Returned to assessment.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Approve(int? id, string email, string pwd, string? notes = null,
            DateTime? approvalExpiry = null, string? approvalExpiryReason = null)
        {
            if (id == null) return NotFound();
            var expiryError = ValidateApprovalExpiry(approvalExpiry, approvalExpiryReason);
            if (expiryError != null)
            {
                TempData["WorkflowError"] = expiryError;
                return RedirectToAction(nameof(Details), new { id });
            }
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var supplier = await _context.Supplier
                .Include(s => s.Documents)
                .Include(s => s.QualityRecords)
                .Include(s => s.Addresses)
                .Include(s => s.SupplierProducts).ThenInclude(sp => sp.Product)
                .Include(s => s.SupplierMaterials).ThenInclude(sm => sm.Material)
                .Include(s => s.SupplierServices).ThenInclude(ss => ss.Service)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (supplier != null)
            {
                if (!TransitionAllowed(supplier, "approve", out var rejected, SupplierStatus.UnderAssessment)) return rejected;
                var fromStatus = supplier.Status.ToString();

                // Calculate next review date from the frequency already set in the Edit view
                supplier.NextReviewDate = supplier.ReviewFrequency switch
                {
                    ReviewFrequency.Annual      => DateTime.UtcNow.AddYears(1),
                    ReviewFrequency.Biannual    => DateTime.UtcNow.AddMonths(6),
                    ReviewFrequency.ThreeYearly => DateTime.UtcNow.AddYears(3),
                    ReviewFrequency.Discretional => supplier.NextReviewDate, // preserved from Edit view
                    _ => null
                };

                // GMP: every approval must carry a periodic re-evaluation point
                if (!HasFutureReviewDate(supplier))
                {
                    TempData["WorkflowError"] = "Cannot approve: a future next review date is required. For a Discretional review frequency, "
                        + "return the supplier to assessment and set the next review date in Edit.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                string toStatus;
                supplier.Status = SupplierStatus.Approved;
                supplier.ApprovalStatus = SupplierStatus.Approved;
                if (supplier.SupplierType != SupplierType.Service)
                    supplier.TestingRequirement = TestingRequirement.FullTesting;
                toStatus = "Approved";

                supplier.ApprovedBy = email;
                supplier.ApprovedOn = DateTime.UtcNow;
                supplier.EffectiveDate = DateTime.UtcNow;
                supplier.ExpiryDate = approvalExpiry?.Date;   // each approval sets its own validity (blank = until next review)
                supplier.ApprovalExpiryReason = approvalExpiry.HasValue ? approvalExpiryReason!.Trim() : null;
                await _context.SaveChangesAsync();
                await CreateSnapshot(supplier, email, supplier.ReviewTrigger);
                await AddAuditLog(supplier.Id, "Approved", fromStatus, toStatus, WithExpiryNote(notes, supplier.ExpiryDate, supplier.ApprovalExpiryReason));
                TempData["Success"] = $"Supplier {toStatus} successfully.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Qualify(int? id, string email, string pwd,
            string? qualJustification, QualificationBasis? qualBasis,
            string? qualRefDoc, string? qualificationComments,
            SupplierRiskLevel? riskLevel, string? riskJustification,
            DateTime? approvalExpiry = null, string? approvalExpiryReason = null)
        {
            if (id == null) return NotFound();
            var expiryError = ValidateApprovalExpiry(approvalExpiry, approvalExpiryReason);
            if (expiryError != null)
            {
                TempData["WorkflowError"] = expiryError;
                return RedirectToAction(nameof(Details), new { id });
            }
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var supplier = await _context.Supplier
                .Include(s => s.Documents)
                .Include(s => s.QualityRecords)
                .Include(s => s.Addresses)
                .Include(s => s.SupplierProducts).ThenInclude(sp => sp.Product)
                .Include(s => s.SupplierMaterials).ThenInclude(sm => sm.Material)
                .Include(s => s.SupplierServices).ThenInclude(ss => ss.Service)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (supplier != null)
            {
                if (!TransitionAllowed(supplier, "qualify", out var rejected, SupplierStatus.Approved)) return rejected;

                if (!supplier.QualificationRequired)
                {
                    TempData["WorkflowError"] = "Qualification is not required for this supplier.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                // GMP: the qualified status must also carry a periodic re-evaluation point
                if (!HasFutureReviewDate(supplier))
                {
                    TempData["WorkflowError"] = "Cannot qualify: the supplier has no future next review date. Start a new review to re-establish the review schedule.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                var fromStatus = supplier.Status.ToString();
                supplier.Status = SupplierStatus.Qualified;
                supplier.ApprovalStatus = SupplierStatus.Qualified;
                supplier.QualifiedBy = email;
                supplier.QualifiedOn = DateTime.UtcNow;
                supplier.EffectiveDate = DateTime.UtcNow;
                if (supplier.SupplierType != SupplierType.Service)
                    supplier.TestingRequirement = TestingRequirement.ReducedTesting;
                supplier.QualificationJustification = qualJustification;
                supplier.QualificationBasis = qualBasis;
                supplier.QualificationReferenceDoc = qualRefDoc;
                supplier.QualificationComments = qualificationComments;
                if (riskLevel.HasValue) supplier.RiskLevel = riskLevel.Value;
                if (!string.IsNullOrWhiteSpace(riskJustification)) supplier.RiskJustification = riskJustification;
                supplier.ExpiryDate = approvalExpiry?.Date;
                supplier.ApprovalExpiryReason = approvalExpiry.HasValue ? approvalExpiryReason!.Trim() : null;
                await _context.SaveChangesAsync();
                await CreateSnapshot(supplier, email, supplier.ReviewTrigger);
                await AddAuditLog(supplier.Id, "Qualified", fromStatus, "Qualified", WithExpiryNote(null, supplier.ExpiryDate, supplier.ApprovalExpiryReason));
                TempData["Success"] = "Supplier progressed to Qualified.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Suspend(int? id, string email, string pwd, string? suspensionReason = null)
        {
            if (id == null) return NotFound();
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var supplier = await _context.Supplier
                .Include(s => s.Documents)
                .Include(s => s.QualityRecords)
                .Include(s => s.Addresses)
                .Include(s => s.SupplierProducts).ThenInclude(sp => sp.Product)
                .Include(s => s.SupplierMaterials).ThenInclude(sm => sm.Material)
                .Include(s => s.SupplierServices).ThenInclude(ss => ss.Service)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (supplier != null)
            {
                if (!TransitionAllowed(supplier, "suspend", out var rejected, SupplierStatus.Approved, SupplierStatus.Qualified, SupplierStatus.PendingSuspension)) return rejected;
                var fromStatus = supplier.Status.ToString();
                supplier.Status = SupplierStatus.Suspended;
                supplier.ApprovalStatus = SupplierStatus.Suspended;
                supplier.SuspendedBy = email;
                supplier.SuspendedOn = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(suspensionReason))
                    supplier.SuspensionReason = suspensionReason;
                await _context.SaveChangesAsync();
                await CreateSnapshot(supplier, email, supplier.ReviewTrigger);
                await AddAuditLog(supplier.Id, "Suspended", fromStatus, "Suspended", suspensionReason);
                TempData["Success"] = "Supplier suspended.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Cancel(int? id, string email, string pwd, string? cancellationReason)
        {
            if (id == null) return NotFound();
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var supplier = await _context.Supplier
                .Include(s => s.Documents)
                .Include(s => s.QualityRecords)
                .Include(s => s.Addresses)
                .Include(s => s.SupplierProducts).ThenInclude(sp => sp.Product)
                .Include(s => s.SupplierMaterials).ThenInclude(sm => sm.Material)
                .Include(s => s.SupplierServices).ThenInclude(ss => ss.Service)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (supplier != null)
            {
                if (!TransitionAllowed(supplier, "cancel", out var rejected, SupplierStatus.Approved, SupplierStatus.Qualified, SupplierStatus.PendingCancellation)) return rejected;
                var fromStatus = supplier.Status.ToString();
                supplier.Status = SupplierStatus.Cancelled;
                supplier.ApprovalStatus = SupplierStatus.Cancelled;
                supplier.CancelledBy = email;
                supplier.CancelledOn = DateTime.UtcNow;
                supplier.CancellationReason = cancellationReason;
                await _context.SaveChangesAsync();
                await CreateSnapshot(supplier, email);
                await AddAuditLog(supplier.Id, "Cancelled", fromStatus, "Cancelled", cancellationReason);
                TempData["Success"] = "Supplier cancelled.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Reactivate(int? id, string email, string pwd, string? notes = null)
        {
            if (id == null) return NotFound();
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var supplier = await _context.Supplier.FindAsync(id);
            if (supplier != null)
            {
                if (!TransitionAllowed(supplier, "reactivate", out var rejected, SupplierStatus.Suspended)) return rejected;
                var fromStatus = supplier.Status.ToString();
                supplier.Status = SupplierStatus.Pending;
                supplier.ApprovalStatus = null;
                supplier.SuspendedBy = null;
                supplier.SuspendedOn = null;
                supplier.SuspensionReason = null;
                await _context.SaveChangesAsync();
                await AddAuditLog(supplier.Id, "Reactivated", fromStatus, "Pending", notes);
                TempData["Success"] = "Supplier reactivated and returned to pending assessment.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // ============================================================
        // SNAPSHOT HELPER
        // ============================================================

        private async Task CreateSnapshot(Supplier supplier, string? completedBy,
            ReviewTrigger? reviewTrigger = null, string? reasonForReview = null,
            int? linkedQRId = null, string? linkedQRCode = null, string? linkedQRTitle = null)
        {
            var linkedItems = new List<string>();
            if (supplier.SupplierProducts != null)
                linkedItems.AddRange(supplier.SupplierProducts.Select(x => x.Product?.Name ?? ""));
            if (supplier.SupplierMaterials != null)
                linkedItems.AddRange(supplier.SupplierMaterials.Select(x => x.Material?.Name ?? ""));
            if (supplier.SupplierServices != null)
                linkedItems.AddRange(supplier.SupplierServices.Select(x => x.Service?.Name ?? ""));

            var snapshot = new SupplierSnapshot
            {
                SupplierId = supplier.Id,
                SnapshotDate = DateTime.UtcNow,
                CompletedBy = completedBy,
                SupplierCode = supplier.SupplierCode,
                LegalName = supplier.LegalName,
                TradingName = supplier.TradingName,
                LegalId = supplier.LegalId,
                Website = supplier.Website,
                Street = supplier.Street,
                City = supplier.City,
                State = supplier.State,
                PostCode = supplier.PostCode,
                Country = supplier.Country,
                ContactName = supplier.ContactName,
                ContactEmail = supplier.ContactEmail,
                ContactPhone = supplier.ContactPhone,
                SupplierType = supplier.SupplierType,
                LinkedItems = string.Join(", ", linkedItems.Where(x => !string.IsNullOrEmpty(x))),
                RiskLevel = supplier.RiskLevel,
                RiskJustification = supplier.RiskJustification,
                AssessedBy = supplier.AssessedBy,
                AssessedOn = supplier.AssessedOn,
                ApprovalStatus = supplier.ApprovalStatus ?? SupplierStatus.Pending,
                ApprovedBy = supplier.ApprovedBy,
                ApprovedOn = supplier.ApprovedOn,
                ApprovalComments = supplier.ApprovalComments,
                ReviewFrequency = supplier.ReviewFrequency,
                QualificationRequired = supplier.QualificationRequired,
                NextReviewDate = supplier.NextReviewDate,
                ExpiryDate = supplier.ExpiryDate,
                ApprovalExpiryReason = supplier.ApprovalExpiryReason,
                QualifiedBy = supplier.QualifiedBy,
                QualifiedOn = supplier.QualifiedOn,
                QualificationBasis = supplier.QualificationBasis,
                QualificationJustification = supplier.QualificationJustification,
                QualificationReferenceDoc = supplier.QualificationReferenceDoc,
                QualificationComments = supplier.QualificationComments,
                SuspendedBy = supplier.SuspendedBy,
                SuspendedOn = supplier.SuspendedOn,
                SuspensionReason = supplier.SuspensionReason,
                CancelledBy = supplier.CancelledBy,
                CancelledOn = supplier.CancelledOn,
                CancellationReason = supplier.CancellationReason,
                ReviewTrigger = reviewTrigger,
                ReasonForReview = reasonForReview,
                LinkedQualityRecordId = linkedQRId,
                LinkedQualityRecordCode = linkedQRCode,
                LinkedQualityRecordTitle = linkedQRTitle
            };

            _context.SupplierSnapshot.Add(snapshot);
            await _context.SaveChangesAsync();

            if (supplier.Addresses != null)
            {
                foreach (var addr in supplier.Addresses)
                {
                    _context.SupplierSnapshotAddress.Add(new SupplierSnapshotAddress
                    {
                        SnapshotId = snapshot.Id,
                        Street = addr.Street,
                        City = addr.City,
                        State = addr.State,
                        PostCode = addr.PostCode,
                        Country = addr.Country,
                        Notes = addr.Notes
                    });
                }
            }

            if (supplier.Documents != null)
            {
                foreach (var doc in supplier.Documents)
                {
                    _context.SupplierSnapshotDocument.Add(new SupplierSnapshotDocument
                    {
                        SnapshotId = snapshot.Id,
                        DocumentType = doc.DocumentType,
                        FileName = doc.FileName,
                        FilePath = doc.FilePath,
                        ContentType = doc.ContentType,
                        Description = doc.Description,
                        IssueDate = doc.IssueDate,
                        ExpiryDate = doc.ExpiryDate,
                        UploadedBy = doc.UploadedBy,
                        UploadedDate = doc.UploadedDate
                    });
                }
            }

            if (supplier.QualityRecords != null)
            {
                var devDict = await _context.Deviation.ToDictionaryAsync(x => x.Id, x => x.DeviationId + "||" + x.Title);
                var capaDict = await _context.Capa.ToDictionaryAsync(x => x.Id, x => x.CapaId + "||" + x.Title);
                var changeDict = await _context.Change.ToDictionaryAsync(x => x.Id, x => x.ChangeId + "||" + x.Title);
                var complaintDict = await _context.Complaint.ToDictionaryAsync(x => x.Id, x => x.ComplaintId + "||" + x.Title);
                var riskDict = await _context.Risk.ToDictionaryAsync(x => x.Id, x => x.RiskId + "||" + x.Title);
                var auditDict = await _context.Audit.ToDictionaryAsync(x => x.Id, x => x.AuditId + "||" + x.Title);

                foreach (var qr in supplier.QualityRecords)
                {
                    string? code = null, title = null;
                    if (qr.DeviationId.HasValue && devDict.ContainsKey(qr.DeviationId.Value)) { var p = devDict[qr.DeviationId.Value].Split("||"); code = p[0]; title = p.Length > 1 ? p[1] : null; }
                    else if (qr.CapaId.HasValue && capaDict.ContainsKey(qr.CapaId.Value)) { var p = capaDict[qr.CapaId.Value].Split("||"); code = p[0]; title = p.Length > 1 ? p[1] : null; }
                    else if (qr.ChangeId.HasValue && changeDict.ContainsKey(qr.ChangeId.Value)) { var p = changeDict[qr.ChangeId.Value].Split("||"); code = p[0]; title = p.Length > 1 ? p[1] : null; }
                    else if (qr.ComplaintId.HasValue && complaintDict.ContainsKey(qr.ComplaintId.Value)) { var p = complaintDict[qr.ComplaintId.Value].Split("||"); code = p[0]; title = p.Length > 1 ? p[1] : null; }
                    else if (qr.RiskId.HasValue && riskDict.ContainsKey(qr.RiskId.Value)) { var p = riskDict[qr.RiskId.Value].Split("||"); code = p[0]; title = p.Length > 1 ? p[1] : null; }
                    else if (qr.AuditId.HasValue && auditDict.ContainsKey(qr.AuditId.Value)) { var p = auditDict[qr.AuditId.Value].Split("||"); code = p[0]; title = p.Length > 1 ? p[1] : null; }

                    _context.SupplierSnapshotQualityRecord.Add(new SupplierSnapshotQualityRecord
                    {
                        SnapshotId = snapshot.Id,
                        RecordType = qr.RecordType,
                        DeviationId = qr.DeviationId,
                        CapaId = qr.CapaId,
                        ChangeId = qr.ChangeId,
                        ComplaintId = qr.ComplaintId,
                        RiskId = qr.RiskId,
                        AuditId = qr.AuditId,
                        RecordCode = code,
                        RecordTitle = title,
                        Notes = qr.Notes,
                        LinkedBy = qr.LinkedBy,
                        LinkedOn = qr.LinkedOn
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        // ============================================================
        // DOCUMENTS
        // ============================================================

        public async Task<IActionResult> Documents(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Supplier
                .Include(s => s.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (supplier == null) return NotFound();

            if (supplier.Documents != null)
            {
                foreach (var doc in supplier.Documents)
                {
                    if (doc.ExpiryDate.HasValue)
                    {
                        var daysLeft = (doc.ExpiryDate.Value - DateTime.UtcNow).TotalDays;
                        doc.DocumentStatus = daysLeft < 0 ? SupplierDocumentStatus.Expired : SupplierDocumentStatus.Valid;
                    }
                    else
                    {
                        doc.DocumentStatus = SupplierDocumentStatus.Valid;
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.SupplierDocument.DeletedOnly().Where(f => f.SupplierId == id).ToListAsync();
            return View(supplier);
        }

        // Addresses

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> AddAddress(int supplierId, string? street, string? city, string? state, string? postCode, string? country, string? notes)
        {
            if (string.IsNullOrWhiteSpace(street) && string.IsNullOrWhiteSpace(city) &&
                string.IsNullOrWhiteSpace(state) && string.IsNullOrWhiteSpace(postCode) &&
                string.IsNullOrWhiteSpace(country) && string.IsNullOrWhiteSpace(notes))
            {
                TempData["Error"] = "At least one address field is required.";
                TempData["ActiveTab"] = "addresses";
                return RedirectToAction(nameof(Details), new { id = supplierId });
            }

            var supplier = await _context.Supplier.FindAsync(supplierId);
            if (supplier == null) return NotFound();
            if (supplier.Status != SupplierStatus.Pending)
            {
                TempData["Error"] = "Addresses can only be added to suppliers in Pending status.";
                return RedirectToAction(nameof(Details), new { id = supplierId });
            }

            _context.SupplierAddress.Add(new SupplierAddress
            {
                SupplierId = supplierId,
                Street = street,
                City = city,
                State = state,
                PostCode = postCode,
                Country = country,
                Notes = notes
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Address added.";
            TempData["ActiveTab"] = "addresses";
            return RedirectToAction(nameof(Details), new { id = supplierId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> DeleteAddress(int id, int supplierId)
        {
            var address = await _context.SupplierAddress.FindAsync(id);
            if (address != null)
            {
                if (address.SupplierId != supplierId) return Forbid();
                var supplier = await _context.Supplier.FindAsync(supplierId);
                if (supplier == null || supplier.Status != SupplierStatus.Pending)
                {
                    TempData["Error"] = "Addresses can only be removed from suppliers in Pending status.";
                    TempData["ActiveTab"] = "addresses";
                    return RedirectToAction(nameof(Details), new { id = supplierId });
                }
                _context.SupplierAddress.Remove(address);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Address removed.";
            }

            TempData["ActiveTab"] = "addresses";
            return RedirectToAction(nameof(Details), new { id = supplierId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> UploadDocument(int supplierId, IFormFile file,
            SupplierDocumentType documentType, string? description, DateTime? issueDate, DateTime? expiryDate)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Documents), new { id = supplierId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, Excel, ZIP, JPG, PNG.";
                return RedirectToAction(nameof(Documents), new { id = supplierId });
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File size exceeds the 25 MB limit.";
                return RedirectToAction(nameof(Documents), new { id = supplierId });
            }

            var supplier = await _context.Supplier.FindAsync(supplierId);
            if (supplier == null) return NotFound();

            if (DocumentsLocked(supplier.Status))
            {
                TempData["Error"] = $"Documents are locked while the supplier is {EnumHelper.Display(supplier.Status)} — start a new review to add documents.";
                return RedirectToAction(nameof(Documents), new { id = supplierId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "suppliers", supplierId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var doc = new SupplierDocument
            {
                SupplierId = supplierId,
                DocumentType = documentType,
                FileName = file.FileName,
                FilePath = $"/uploads/suppliers/{supplierId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                Description = description,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                DocumentStatus = SupplierDocumentStatus.Valid,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow
            };

            _context.SupplierDocument.Add(doc);
            await _context.SaveChangesAsync();
            await AddAuditLog(supplierId, "Document uploaded", null, null, $"{file.FileName} ({EnumHelper.Display(documentType)})");

            TempData["Success"] = $"Document '{file.FileName}' uploaded successfully.";
            return RedirectToAction(nameof(Documents), new { id = supplierId });
        }

        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> DownloadDocument(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();
            var doc = await _context.SupplierDocument.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (doc == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToAction(nameof(Documents), new { id = doc.SupplierId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, doc.FileHash))
            {
                await AddAuditLog(doc.SupplierId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction(nameof(Documents), new { id = doc.SupplierId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Supplier",
                doc.SupplierId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser,CommercialUser")]
        public async Task<IActionResult> DeleteDocument(int id, int supplierId, string? deletionReason)
        {
            var doc = await _context.SupplierDocument.FindAsync(id);
            if (doc != null)
            {
                // Resolve the parent from the document itself — never trust the posted id.
                supplierId = doc.SupplierId;

                var supplier = await _context.Supplier.FindAsync(supplierId);
                if (supplier != null && DocumentsLocked(supplier.Status))
                {
                    TempData["Error"] = $"Documents are locked while the supplier is {EnumHelper.Display(supplier.Status)} — start a new review to change documents.";
                    return RedirectToAction(nameof(Documents), new { id = supplierId });
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete a document.";
                    return RedirectToAction(nameof(Documents), new { id = supplierId });
                }

                // Soft delete: the row and file are retained for traceability.
                doc.IsDeleted      = true;
                doc.DeletedBy      = User.Identity?.Name ?? "Unknown";
                doc.DeletedOn      = DateTime.UtcNow;
                doc.DeletionReason = deletionReason.Trim();
                await AddAuditLog(supplierId, "Document deleted", null, null, $"{doc.FileName} — Reason: {doc.DeletionReason}");
                TempData["Success"] = "Document deleted.";
            }
            return RedirectToAction(nameof(Documents), new { id = supplierId });
        }

        // ============================================================
        // REVIEWS
        // ============================================================

        public async Task<IActionResult> DownloadSnapshotDocument(int? id)
        {
            if (id == null) return NotFound();
            var doc = await _context.SupplierSnapshotDocument.FindAsync(id);
            if (doc == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToAction(nameof(Reviews), new { id = doc.SnapshotId });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }

            var wmCtxSnap = new WatermarkContext(
                WatermarkKind.Attachment, null, "Supplier",
                doc.SnapshotId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarkedSnap = WatermarkService.Apply(
                memory.ToArray(), Path.GetExtension(doc.FileName), wmCtxSnap);
            return File(watermarkedSnap, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        public async Task<IActionResult> SnapshotDetails(int? id)
        {
            if (id == null) return NotFound();
            var snapshot = await _context.SupplierSnapshot
                .Include(s => s.Documents)
                .Include(s => s.QualityRecords)
                .Include(s => s.Addresses)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (snapshot == null) return NotFound();
            return View(snapshot);
        }

        public async Task<IActionResult> Reviews(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Supplier
                .Include(s => s.Reviews)
                .Include(s => s.Snapshots)
                    .ThenInclude(sn => sn.Documents)
                .Include(s => s.Snapshots)
                    .ThenInclude(sn => sn.QualityRecords)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> CreateReview(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Supplier.FindAsync(id);
            if (supplier == null) return NotFound();
            ViewBag.SupplierId = id;
            ViewBag.SupplierCode = supplier.SupplierCode;
            ViewBag.LegalName = supplier.LegalName;
            // A new review starts only from a completed workflow outcome (as offered on the Reviews page)
            if (!TransitionAllowed(supplier, "start a new review", out var rejected,
                    SupplierStatus.Approved, SupplierStatus.Qualified, SupplierStatus.Suspended, SupplierStatus.Cancelled))
                return rejected;

            ViewBag.CurrentStatus = supplier.Status;
            ViewBag.IsReassessment = supplier.ApprovalStatus == SupplierStatus.Approved
                                  || supplier.ApprovalStatus == SupplierStatus.Qualified;
            ViewBag.ApprovalStatus = supplier.ApprovalStatus;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> CreateReview(int supplierId,
            [Bind("ReviewTrigger,ReviewSummary")] SupplierReview review,
            int? linkedQRId = null, string? linkedQRCode = null, string? linkedQRTitle = null)
        {
            var supplier = await _context.Supplier.FindAsync(supplierId);
            if (supplier == null) return NotFound();
            if (!TransitionAllowed(supplier, "start a new review", out var rejected,
                    SupplierStatus.Approved, SupplierStatus.Qualified, SupplierStatus.Suspended, SupplierStatus.Cancelled))
                return rejected;

            review.SupplierId = supplierId;
            review.StatusBefore = supplier.ApprovalStatus ?? supplier.Status;
            review.StatusAfter = supplier.ApprovalStatus ?? supplier.Status;
            review.ReviewDate = DateTime.UtcNow;
            review.ReviewedBy = User.Identity?.Name ?? "Unknown";
            review.ReviewedOn = DateTime.UtcNow;
            review.ReviewStatus = SupplierReviewStatus.Draft;

            ModelState.Clear();

            _context.SupplierReview.Add(review);

            // Last review = when this review actually started (the scheduled NextReviewDate may lie in the future)
            supplier.LastReviewDate = review.ReviewDate;
            supplier.NextReviewDate = null;
            supplier.Status = SupplierStatus.Pending;
            supplier.ReviewTrigger = review.ReviewTrigger;
            supplier.AssessedBy = null;
            supplier.AssessedOn = null;
            supplier.RiskLevel = SupplierRiskLevel.NotAssessed;
            supplier.RiskJustification = null;
            supplier.ReviewFrequency = default;
            supplier.ApprovalComments = null;
            supplier.QualificationRequired = false;
            supplier.QualificationJustification = null;

            // Current documents move out of the live record for the new cycle. Soft-delete them
            // (rather than remove) so documents added after the last history snapshot are not lost.
            var existingDocs = await _context.SupplierDocument.Where(d => d.SupplierId == supplierId).ToListAsync();
            foreach (var d in existingDocs)
            {
                d.IsDeleted      = true;
                d.DeletedBy      = User.Identity?.Name ?? "Unknown";
                d.DeletedOn      = DateTime.UtcNow;
                d.DeletionReason = "Archived to supplier history — new review started";
            }
            var existingQRs = _context.SupplierQualityRecord.Where(q => q.SupplierId == supplierId);
            _context.SupplierQualityRecord.RemoveRange(existingQRs);

            await _context.SaveChangesAsync();
            await AddAuditLog(supplierId, "Review Initiated", supplier.ApprovalStatus?.ToString(), "Pending");
            TempData["Success"] = "New review started successfully.";
            return RedirectToAction(nameof(Details), new { id = supplierId });
        }

        // ============================================================
        // QUALITY RECORDS
        // ============================================================

        public async Task<IActionResult> QualityRecords(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Supplier
                .Include(s => s.QualityRecords)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (supplier == null) return NotFound();

            ViewBag.Deviations = await _context.Deviation.ToDictionaryAsync(x => x.Id, x => x.DeviationId + "||" + x.Title);
            ViewBag.Capas = await _context.Capa.ToDictionaryAsync(x => x.Id, x => x.CapaId + "||" + x.Title);
            ViewBag.Changes = await _context.Change.ToDictionaryAsync(x => x.Id, x => x.ChangeId + "||" + x.Title);
            ViewBag.Complaints = await _context.Complaint.ToDictionaryAsync(x => x.Id, x => x.ComplaintId + "||" + x.Title);
            ViewBag.Risks = await _context.Risk.ToDictionaryAsync(x => x.Id, x => x.RiskId + "||" + x.Title);
            ViewBag.Audits = await _context.Audit.ToDictionaryAsync(x => x.Id, x => x.AuditId + "||" + x.Title);

            return View(supplier);
        }

        [HttpGet]
        public async Task<IActionResult> GetQualityRecords(QualityRecordType type)
        {
            object results;
            switch (type)
            {
                case QualityRecordType.Deviation:
                    results = await _context.Deviation.OrderByDescending(x => x.Id)
                        .Select(x => new { id = x.Id, code = x.DeviationId, title = x.Title }).ToListAsync();
                    break;
                case QualityRecordType.CAPA:
                    results = await _context.Capa.OrderByDescending(x => x.Id)
                        .Select(x => new { id = x.Id, code = x.CapaId, title = x.Title }).ToListAsync();
                    break;
                case QualityRecordType.Change:
                    results = await _context.Change.OrderByDescending(x => x.Id)
                        .Select(x => new { id = x.Id, code = x.ChangeId, title = x.Title }).ToListAsync();
                    break;
                case QualityRecordType.Complaint:
                    results = await _context.Complaint.OrderByDescending(x => x.Id)
                        .Select(x => new { id = x.Id, code = x.ComplaintId, title = x.Title }).ToListAsync();
                    break;
                case QualityRecordType.Risk:
                    results = await _context.Risk.OrderByDescending(x => x.Id)
                        .Select(x => new { id = x.Id, code = x.RiskId, title = x.Title }).ToListAsync();
                    break;
                case QualityRecordType.Audit:
                    results = await _context.Audit.OrderByDescending(x => x.Id)
                        .Select(x => new { id = x.Id, code = x.AuditId, title = x.Title }).ToListAsync();
                    break;
                default:
                    results = new List<object>();
                    break;
            }
            return Json(results);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> LinkQualityRecord(int supplierId, QualityRecordType recordType,
            int? deviationId, int? capaId, int? changeId, int? complaintId, int? riskId, int? auditId, string? notes)
        {
            var supplier = await _context.Supplier.FindAsync(supplierId);
            if (supplier == null) return NotFound();
            if (DocumentsLocked(supplier.Status))
            {
                TempData["Error"] = $"Quality records are locked while the supplier is {EnumHelper.Display(supplier.Status)} — start a new review to change them.";
                return RedirectToAction(nameof(QualityRecords), new { id = supplierId });
            }

            var record = new SupplierQualityRecord
            {
                SupplierId = supplierId,
                RecordType = recordType,
                DeviationId = deviationId,
                CapaId = capaId,
                ChangeId = changeId,
                ComplaintId = complaintId,
                RiskId = riskId,
                AuditId = auditId,
                Notes = notes,
                LinkedOn = DateTime.UtcNow,
                LinkedBy = User.Identity?.Name ?? "Unknown"
            };

            _context.SupplierQualityRecord.Add(record);
            await _context.SaveChangesAsync();
            await AddAuditLog(supplierId, "Quality record linked", null, null, await QualityRecordLabel(record));
            TempData["Success"] = "Quality record linked.";
            return RedirectToAction(nameof(QualityRecords), new { id = supplierId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> DeleteQualityRecord(int id, int supplierId)
        {
            var record = await _context.SupplierQualityRecord.FindAsync(id);
            if (record != null)
            {
                // Resolve the parent from the link itself — never trust the posted id.
                supplierId = record.SupplierId;
                var supplier = await _context.Supplier.FindAsync(supplierId);
                if (supplier != null && DocumentsLocked(supplier.Status))
                {
                    TempData["Error"] = $"Quality records are locked while the supplier is {EnumHelper.Display(supplier.Status)} — start a new review to change them.";
                    return RedirectToAction(nameof(QualityRecords), new { id = supplierId });
                }
                var label = await QualityRecordLabel(record);
                _context.SupplierQualityRecord.Remove(record);
                await AddAuditLog(supplierId, "Quality record unlinked", null, null, label);   // saves the removal too
            }
            return RedirectToAction(nameof(QualityRecords), new { id = supplierId });
        }

        // Human-readable identity of a linked quality record for the audit trail, e.g. "Deviation DEV-0012".
        private async Task<string> QualityRecordLabel(SupplierQualityRecord r)
        {
            string? code = r.RecordType switch
            {
                QualityRecordType.Deviation => await _context.Deviation!.Where(x => x.Id == r.DeviationId).Select(x => x.DeviationId).FirstOrDefaultAsync(),
                QualityRecordType.CAPA      => await _context.Capa!.Where(x => x.Id == r.CapaId).Select(x => x.CapaId).FirstOrDefaultAsync(),
                QualityRecordType.Change    => await _context.Change!.Where(x => x.Id == r.ChangeId).Select(x => x.ChangeId).FirstOrDefaultAsync(),
                QualityRecordType.Complaint => await _context.Complaint.Where(x => x.Id == r.ComplaintId).Select(x => x.ComplaintId).FirstOrDefaultAsync(),
                QualityRecordType.Risk      => await _context.Risk.Where(x => x.Id == r.RiskId).Select(x => x.RiskId).FirstOrDefaultAsync(),
                QualityRecordType.Audit     => await _context.Audit.Where(x => x.Id == r.AuditId).Select(x => x.AuditId).FirstOrDefaultAsync(),
                _ => null
            };
            return $"{r.RecordType} {code ?? "(unknown)"}";
        }

        public async Task<IActionResult> ExportSupplierDetail(int id)
        {
            if (_context.Supplier == null) return NotFound();
            var supplier = await _context.Supplier
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();
            if (supplier == null) return NotFound();

            var fields = new List<(string, string?)>
            {
                ("Supplier Code",         supplier.SupplierCode),
                ("Legal Name",            supplier.LegalName),
                ("Trading Name",          supplier.TradingName),
                ("Status",                supplier.Status.ToString()),
                ("Supplier Type",         supplier.SupplierType.ToString()),
                ("Risk Level",            supplier.RiskLevel.ToString()),
                ("Country",               supplier.Country),
                ("City",                  supplier.City),
                ("Contact Name",          supplier.ContactName),
                ("Contact Email",         supplier.ContactEmail),
                ("Contact Phone",         supplier.ContactPhone),
                ("Created By",            supplier.CreatedBy),
                ("Created On",            supplier.CreatedOn.ToSite().ToShortDateString()),
                ("Assessed By",           supplier.AssessedBy),
                ("Assessed On",           supplier.AssessedOn.ToSite()?.ToShortDateString()),
                ("Risk Justification",    supplier.RiskJustification),
                ("Review Frequency",      supplier.ReviewFrequency.ToString()),
                ("Qualification Required", supplier.QualificationRequired ? "Yes" : "No"),
                ("Approved By",           supplier.ApprovedBy),
                ("Approved On",           supplier.ApprovedOn.ToSite()?.ToShortDateString()),
                ("Effective Date",        supplier.EffectiveDate.ToSite()?.ToShortDateString()),
                ("Next Review",           supplier.NextReviewDate?.ToShortDateString()),
                ("Last Review",           supplier.LastReviewDate?.ToShortDateString()),
                ("Qualified By",          supplier.QualifiedBy),
                ("Qualified On",          supplier.QualifiedOn.ToSite()?.ToShortDateString()),
            };

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Supplier Detail Report", supplier.SupplierCode, supplier.Status.ToString(), fields,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Supplier-{supplier.SupplierCode}.pdf");
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            if (_context.Supplier == null) return NotFound();
            var supplier = _context.Supplier
                .Where(x => x.Id == id)
                .FirstOrDefault();
            if (supplier == null) return NotFound();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Supplier Code,Legal Name,Trading Name,Status,Supplier Type,Risk Level,Country,City,Contact Name,Contact Email,Assessed By,Assessed On,Review Frequency,Qualification Required,Approved By,Approved On,Effective Date,Next Review,Qualified By,Qualified On");
            builder.AppendLine($"{supplier.SupplierCode},{supplier.LegalName},{supplier.TradingName ?? ""},{supplier.Status},{supplier.SupplierType},{supplier.RiskLevel},{supplier.Country ?? ""},{supplier.City ?? ""},{supplier.ContactName ?? ""},{supplier.ContactEmail ?? ""},{supplier.AssessedBy ?? ""},{supplier.AssessedOn.ToSite()?.ToShortDateString() ?? ""},{supplier.ReviewFrequency},{(supplier.QualificationRequired ? "Yes" : "No")},{supplier.ApprovedBy ?? ""},{supplier.ApprovedOn.ToSite()?.ToShortDateString() ?? ""},{supplier.EffectiveDate.ToSite()?.ToShortDateString() ?? ""},{supplier.NextReviewDate?.ToShortDateString() ?? ""},{supplier.QualifiedBy ?? ""},{supplier.QualifiedOn.ToSite()?.ToShortDateString() ?? ""}");

            return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"Supplier-{supplier.SupplierCode}.csv");
        }

        private bool SupplierExists(int id) => _context.Supplier.Any(e => e.Id == id);
    }
}
