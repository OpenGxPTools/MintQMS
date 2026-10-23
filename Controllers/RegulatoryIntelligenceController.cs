using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.LC;
using OpenQMS.Models.RI;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class RegulatoryIntelligenceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly RIWorkflowService _workflow;

        public RegulatoryIntelligenceController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            IPasswordHasher<AppUser> passwordHasher,
            RIWorkflowService workflow)
        {
            _context        = context;
            _userManager    = userManager;
            _passwordHasher = passwordHasher;
            _workflow       = workflow;
        }

        // ── DASHBOARD ─────────────────────────────────────────────────────────

        // GET /RegulatoryIntelligence/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var today = SiteTime.Today;

            var allApps = await _context.RegulatoryApplications
                .Where(x => !x.IsDeleted)
                .Include(x => x.Jurisdiction)
                .Include(x => x.ApplicationType)
                .ToListAsync();

            // Status summary
            ViewBag.TotalApplications = allApps.Count;
            ViewBag.StatusCounts = allApps
                .GroupBy(x => x.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.ActiveCount     = allApps.Count(a => a.Status == RIApplicationStatus.Approved
                                                       || a.Status == RIApplicationStatus.ConditionsActive);
            ViewBag.UnderReviewCount = allApps.Count(a => a.Status == RIApplicationStatus.UnderReview
                                                        || a.Status == RIApplicationStatus.ClockStop);

            // Jurisdiction breakdown
            ViewBag.JurisdictionGroups = allApps
                .GroupBy(a => new { Code = a.Jurisdiction?.Code ?? "?", Auth = a.Jurisdiction?.Authority ?? "?" })
                .Select(g => new { g.Key.Code, g.Key.Auth, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Approaching decisions (UnderReview, target ≤ 30 days)
            ViewBag.ApproachingDecisions = allApps
                .Where(a => a.Status == RIApplicationStatus.UnderReview
                         && a.TargetDecisionDate.HasValue
                         && a.TargetDecisionDate.Value.Date <= today.AddDays(30))
                .OrderBy(a => a.TargetDecisionDate)
                .ToList();

            // Expiring authorisations (≤ 90 days)
            ViewBag.ExpiringApprovals = allApps
                .Where(a => (a.Status == RIApplicationStatus.Approved
                          || a.Status == RIApplicationStatus.ConditionsActive)
                         && a.ExpiryDate.HasValue
                         && a.ExpiryDate.Value.Date <= today.AddDays(90))
                .OrderBy(a => a.ExpiryDate)
                .ToList();

            // Open conditions (outstanding/in-progress), top 10
            var openConditions = await _context.RIConditionsOfApproval
                .Where(c => c.Status == RIConditionStatus.Outstanding
                         || c.Status == RIConditionStatus.InProgress)
                .Include(c => c.Application)
                .ThenInclude(a => a.Jurisdiction)
                .OrderBy(c => c.DueDate)
                .Take(10)
                .ToListAsync();
            ViewBag.OpenConditions    = openConditions;
            // Separate full count (the list is limited to top 10)
            ViewBag.OpenConditionCount = await _context.RIConditionsOfApproval
                .CountAsync(c => c.Status == RIConditionStatus.Outstanding
                              || c.Status == RIConditionStatus.InProgress);

            // Access pathways expiring within 60 days, top 10
            var expiringPathways = await _context.RIAccessPathwayRecords
                .Where(ap => (ap.Status == RIAccessPathwayStatus.Active
                           || ap.Status == RIAccessPathwayStatus.Approved)
                          && ap.ExpiryDate.HasValue
                          && ap.ExpiryDate.Value.Date <= today.AddDays(60))
                .Include(ap => ap.Application)
                .OrderBy(ap => ap.ExpiryDate)
                .Take(10)
                .ToListAsync();
            ViewBag.ExpiringPathways  = expiringPathways;
            ViewBag.ActivePathwayCount = await _context.RIAccessPathwayRecords
                .CountAsync(ap => ap.Status == RIAccessPathwayStatus.Active
                               || ap.Status == RIAccessPathwayStatus.Approved);
            ViewBag.OverdueReportCount = await _context.RIAccessPathwayRecords
                .CountAsync(ap => ap.NextSixMonthlyReportDue.HasValue
                               && ap.NextSixMonthlyReportDue < today);

            // Overdue milestones, top 10
            var overdueMilestones = await _context.RIMilestones
                .Where(m => (m.Status == RIMilestoneStatus.Pending
                          || m.Status == RIMilestoneStatus.Overdue)
                         && m.PlannedDate.HasValue
                         && m.PlannedDate < today)
                .Include(m => m.Application)
                .OrderBy(m => m.PlannedDate)
                .Take(10)
                .ToListAsync();
            ViewBag.OverdueMilestones     = overdueMilestones;
            ViewBag.OverdueMilestoneCount = overdueMilestones.Count;

            // Recent RI audit activity
            var recentLogs = await _context.RIAuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(12)
                .ToListAsync();
            ViewBag.RecentActivity = recentLogs;
            var recentAppIds = recentLogs.Select(l => l.RegulatoryApplicationId).Distinct().ToList();
            var recentAppList = await _context.RegulatoryApplications
                .Where(a => recentAppIds.Contains(a.Id))
                .Select(a => new { a.Id, a.RINumber })
                .ToListAsync();
            ViewBag.RecentActivityApps = recentAppList.ToDictionary(a => a.Id, a => a.RINumber);

            return View();
        }

        // ── EXPORTS ───────────────────────────────────────────────────────────

        // GET /RegulatoryIntelligence/ExportApplications
        public async Task<IActionResult> ExportApplications()
        {
            var apps = await _context.RegulatoryApplications
                .Where(x => !x.IsDeleted)
                .Include(x => x.Jurisdiction)
                .Include(x => x.ApplicationType)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("RI Applications");

            string[] headers =
            {
                "RI Number", "Jurisdiction", "Authority", "Application Type", "Product",
                "Active Substance", "Indication", "Status", "Submission Date",
                "Day 0", "Target Decision Date", "Actual Decision Date",
                "Approval Date", "Expiry Date", "Next Renewal Due",
                "Authority Ref No.", "Internal RA Lead", "Designations",
                "Created By", "Created At"
            };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Resolve Internal RA Lead user-ids to display names for the export
            var raLeadNames = await _userManager.Users
                .ToDictionaryAsync(u => u.Id.ToString(), u => $"{u.FirstName} {u.LastName}".Trim());
            string ResolveRALead(string? id) =>
                string.IsNullOrEmpty(id) ? "" : (raLeadNames.TryGetValue(id, out var n) ? n : id);

            int row = 2;
            foreach (var app in apps)
            {
                var desig = new List<string>();
                if (app.IsOrphanDesignation)   desig.Add("Orphan");
                if (app.IsPriorityReview)      desig.Add("Priority");
                if (app.IsBreakthroughTherapy) desig.Add("Breakthrough");
                if (app.IsPRIME)               desig.Add("PRIME");
                if (app.IsConditionalApproval) desig.Add("Conditional");

                ws.Cell(row, 1).Value  = app.RINumber;
                ws.Cell(row, 2).Value  = app.Jurisdiction?.Name ?? "";
                ws.Cell(row, 3).Value  = app.Jurisdiction?.Authority ?? "";
                ws.Cell(row, 4).Value  = app.ApplicationType?.Name ?? "";
                ws.Cell(row, 5).Value  = app.ProductName;
                ws.Cell(row, 6).Value  = app.ActiveSubstance ?? "";
                ws.Cell(row, 7).Value  = app.Indication ?? "";
                ws.Cell(row, 8).Value  = app.Status.ToString();
                ws.Cell(row, 9).Value  = app.SubmissionDate?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 10).Value = app.Day0Date?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 11).Value = app.TargetDecisionDate?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 12).Value = app.ActualDecisionDate?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 13).Value = app.ApprovalDate?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 14).Value = app.ExpiryDate?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 15).Value = app.NextRenewalDue?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 16).Value = app.AuthorityReferenceNumber ?? "";
                ws.Cell(row, 17).Value = ResolveRALead(app.InternalRALead);
                ws.Cell(row, 18).Value = string.Join(", ", desig);
                ws.Cell(row, 19).Value = app.CreatedBy;
                ws.Cell(row, 20).Value = app.CreatedAt.ToSite().ToString("dd/MM/yyyy HH:mm");
                row++;
            }
            ws.Columns().AdjustToContents(8, 50);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"RI_Applications_{SiteTime.Now:yyyyMMdd}.xlsx");
        }

        // GET /RegulatoryIntelligence/ExportAccessPathways
        public async Task<IActionResult> ExportAccessPathways()
        {
            var pathways = await _context.RIAccessPathwayRecords
                .Include(ap => ap.Application).ThenInclude(a => a.Jurisdiction)
                .Include(ap => ap.SupplyReports)
                .OrderByDescending(ap => ap.CreatedAt)
                .ToListAsync();

            using var wb = new XLWorkbook();

            // Sheet 1 — Access Pathways
            var ws = wb.Worksheets.Add("Access Pathways");
            string[] h1 =
            {
                "AP Number", "RI Application", "Jurisdiction", "Pathway Type",
                "Patient ID", "Prescriber Name", "Prescriber ID", "Status",
                "Approval Letter Ref", "Approval Date", "Expiry Date",
                "Next Report Due", "AE Reporting", "Clinical Justification", "Created By"
            };
            for (int i = 0; i < h1.Length; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = h1[i]; c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                c.Style.Font.FontColor = XLColor.White;
            }
            int r = 2;
            foreach (var ap in pathways)
            {
                ws.Cell(r, 1).Value  = ap.AccessNumber;
                ws.Cell(r, 2).Value  = ap.Application?.RINumber ?? "";
                ws.Cell(r, 3).Value  = ap.Application?.Jurisdiction?.Authority ?? "";
                ws.Cell(r, 4).Value  = ap.PathwayType.ToString();
                ws.Cell(r, 5).Value  = ap.PatientIdentifier ?? "";
                ws.Cell(r, 6).Value  = ap.PrescriberName ?? "";
                ws.Cell(r, 7).Value  = ap.PrescriberId ?? "";
                ws.Cell(r, 8).Value  = ap.Status.ToString();
                ws.Cell(r, 9).Value  = ap.TGAApprovalLetterNumber ?? "";
                ws.Cell(r, 10).Value = ap.ApprovalDate?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(r, 11).Value = ap.ExpiryDate?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(r, 12).Value = ap.NextSixMonthlyReportDue?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(r, 13).Value = ap.AEReportingRequired ? "Yes" : "No";
                ws.Cell(r, 14).Value = ap.ClinicalJustification ?? "";
                ws.Cell(r, 15).Value = ap.CreatedBy;
                r++;
            }
            ws.Columns().AdjustToContents(8, 50);

            // Sheet 2 — Supply Reports
            var ws2 = wb.Worksheets.Add("Supply Reports");
            string[] h2 =
            {
                "AP Number", "Period Start", "Period End", "Qty Supplied", "Unit",
                "Submitted to Authority", "Authority Ref #", "Status", "Notes", "Created By"
            };
            for (int i = 0; i < h2.Length; i++)
            {
                var c = ws2.Cell(1, i + 1);
                c.Value = h2[i]; c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                c.Style.Font.FontColor = XLColor.White;
            }
            int r2 = 2;
            foreach (var ap in pathways)
            {
                foreach (var sr in ap.SupplyReports.OrderByDescending(x => x.PeriodEnd))
                {
                    ws2.Cell(r2, 1).Value  = ap.AccessNumber;
                    ws2.Cell(r2, 2).Value  = sr.PeriodStart.ToString("dd/MM/yyyy");
                    ws2.Cell(r2, 3).Value  = sr.PeriodEnd.ToString("dd/MM/yyyy");
                    ws2.Cell(r2, 4).Value  = sr.QuantitySupplied;
                    ws2.Cell(r2, 5).Value  = sr.Unit ?? "";
                    ws2.Cell(r2, 6).Value  = sr.SubmittedToAuthorityDate?.ToString("dd/MM/yyyy") ?? "";
                    ws2.Cell(r2, 7).Value  = sr.AuthorityReferenceNumber ?? "";
                    ws2.Cell(r2, 8).Value  = sr.Status.ToString();
                    ws2.Cell(r2, 9).Value  = sr.Notes ?? "";
                    ws2.Cell(r2, 10).Value = sr.CreatedBy;
                    r2++;
                }
            }
            ws2.Columns().AdjustToContents(8, 50);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"RI_AccessPathways_{SiteTime.Now:yyyyMMdd}.xlsx");
        }

        // ── INDEX ─────────────────────────────────────────────────────────────

        // GET /RegulatoryIntelligence
        public async Task<IActionResult> Index(string? jurisdiction, string? status, int? year)
        {
            var query = _context.RegulatoryApplications
                .Where(x => !x.IsDeleted)
                .Include(x => x.Jurisdiction)
                .Include(x => x.ApplicationType)
                .Include(x => x.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(jurisdiction))
                query = query.Where(x => x.Jurisdiction.Code == jurisdiction);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<RIApplicationStatus>(status, out var s))
                query = query.Where(x => x.Status == s);

            if (year.HasValue)
                query = query.Where(x => x.RINumber.StartsWith($"RI-{year}-"));

            var applications = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            ViewData["Jurisdictions"] = await _context.RIJurisdictions
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            ViewData["CurrentJurisdiction"] = jurisdiction;
            ViewData["CurrentStatus"]       = status;
            ViewData["CurrentYear"]         = year;

            return View(applications);
        }

        // ── DETAILS ───────────────────────────────────────────────────────────

        // GET /RegulatoryIntelligence/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var app = await _context.RegulatoryApplications
                .Where(x => !x.IsDeleted)
                .Include(x => x.Jurisdiction)
                .Include(x => x.ApplicationType)
                .Include(x => x.Product)
                .Include(x => x.Milestones.OrderBy(m => m.PlannedDate ?? DateTime.MaxValue))
                .Include(x => x.Conditions)
                .Include(x => x.Variations)
                .Include(x => x.AccessPathways)
                .Include(x => x.Documents)
                .Include(x => x.AuditLogs.OrderByDescending(a => a.Timestamp).Take(20))
                .Include(x => x.ApprovalSnapshots.OrderBy(s => s.RenewalCycle))
                .FirstOrDefaultAsync(x => x.Id == id);

            if (app == null) return NotFound();

            // Auto-mark overdue milestones so the view is always current
            var today = SiteTime.Today;
            bool milestoneUpdated = false;
            foreach (var m in app.Milestones)
            {
                if (m.Status == RIMilestoneStatus.Pending
                    && m.PlannedDate.HasValue
                    && m.PlannedDate.Value.Date < today
                    && !m.ActualDate.HasValue)
                {
                    m.Status = RIMilestoneStatus.Overdue;
                    milestoneUpdated = true;
                }
            }
            if (milestoneUpdated) await _context.SaveChangesAsync();

            // Supplier name for display if ManufacturingSupplierId is set
            if (app.ManufacturingSupplierId.HasValue)
            {
                var supplier = await _context.Supplier.FindAsync(app.ManufacturingSupplierId.Value);
                ViewData["ManufacturingSupplierName"] = supplier?.LegalName;
            }

            // Resolve Internal RA Lead user-id to a display name (legacy free-text values fall back as-is).
            // The AppUser key is an int, so only look up numeric ids — a free-text name like "PUPI"
            // would otherwise throw a FormatException inside FindByIdAsync.
            if (!string.IsNullOrEmpty(app.InternalRALead))
            {
                var lead = int.TryParse(app.InternalRALead, out _)
                    ? await _userManager.FindByIdAsync(app.InternalRALead)
                    : null;
                ViewData["RALeadName"] = lead != null
                    ? $"{lead.FirstName} {lead.LastName}".Trim()
                    : app.InternalRALead;
            }

            // Linked LC records (via LCDependency)
            ViewBag.LinkedLCRecords = await _context.LCDependencies
                .Where(d => d.RegulatoryApplicationId == id)
                .Include(d => d.Record).ThenInclude(r => r.LicenceType)
                .OrderBy(d => d.Record.LCNumber)
                .ToListAsync();

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.RIDocuments.DeletedOnly().Where(f => f.RegulatoryApplicationId == id).ToListAsync();
            return View(app);
        }

        // ── CREATE ────────────────────────────────────────────────────────────

        // GET /RegulatoryIntelligence/Create
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Create()
        {
            await PopulateCreateEditViewData();
            return View(new RegulatoryApplication());
        }

        // POST /RegulatoryIntelligence/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Create(RegulatoryApplication model)
        {
            // Remove nav-property validation errors — only scalar fields matter
            ModelState.Remove("Product");
            ModelState.Remove("Jurisdiction");
            ModelState.Remove("ApplicationType");
            ModelState.Remove("ParentApplication");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("RINumber"); // auto-generated; hidden field posts empty string

            if (!ModelState.IsValid)
            {
                await PopulateCreateEditViewData(selectedProductId: model.ProductId, selectedRALeadId: model.InternalRALead);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            model.RINumber  = GenerateRINumber();
            model.Status    = RIApplicationStatus.Strategy;
            model.CreatedBy = user?.UserName ?? User.Identity?.Name ?? "Unknown";
            model.CreatedAt = DateTime.UtcNow;
            model.IsDeleted = false;
            model.IsLocked  = false;

            _context.RegulatoryApplications.Add(model);
            await _context.SaveChangesAsync();

            // Write audit log entry
            _context.RIAuditLogs.Add(new RIAuditLog
            {
                RegulatoryApplicationId = model.Id,
                Action  = "Created",
                NewValue = model.RINumber,
                UserId  = user?.UserName ?? User.Identity?.Name ?? "Unknown",
                Notes   = $"Application {model.RINumber} created."
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Application {model.RINumber} created successfully.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ──────────────────────────────────────────────────────────────

        // GET /RegulatoryIntelligence/Edit/5
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var app = await _context.RegulatoryApplications
                .Include(x => x.Documents)
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (app == null) return NotFound();

            if (RIWorkflowService.IsTerminal(app.Status))
            {
                TempData["Error"] = "This application is closed and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (app.IsLocked)
            {
                TempData["Error"] = "This record is locked and cannot be edited. Create a Variation to make changes.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await PopulateCreateEditViewData(app.JurisdictionId, app.ApplicationTypeId, app.ManufacturingSupplierId, app.ProductId, app.InternalRALead);
            return View(app);
        }

        // POST /RegulatoryIntelligence/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int id, RegulatoryApplication model)
        {
            if (id != model.Id) return NotFound();

            var existing = await _context.RegulatoryApplications
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (existing == null) return NotFound();

            if (RIWorkflowService.IsTerminal(existing.Status))
            {
                TempData["Error"] = "This application is closed and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (existing.IsLocked)
            {
                TempData["Error"] = "This record is locked and cannot be edited. Create a Variation to make changes.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ModelState.Remove("Product");
            ModelState.Remove("Jurisdiction");
            ModelState.Remove("ApplicationType");
            ModelState.Remove("ParentApplication");
            ModelState.Remove("CreatedBy");

            if (!ModelState.IsValid)
            {
                await PopulateCreateEditViewData(model.JurisdictionId, model.ApplicationTypeId, model.ManufacturingSupplierId, model.ProductId, model.InternalRALead);
                return View(model);
            }

            var user    = await _userManager.GetUserAsync(User);
            var userId  = user?.UserName ?? User.Identity?.Name ?? "Unknown";

            // Audit: detect changed fields
            var auditEntries = BuildAuditDiff(existing, model, userId);

            // Update all scalar fields
            existing.ProductId               = model.ProductId;
            existing.JurisdictionId          = model.JurisdictionId;
            existing.ApplicationTypeId       = model.ApplicationTypeId;
            existing.ProductName             = model.ProductName;
            existing.ActiveSubstance         = model.ActiveSubstance;
            existing.Indication              = model.Indication;
            existing.DosageForm              = model.DosageForm;
            existing.Strength                = model.Strength;
            existing.AuthorityReferenceNumber = model.AuthorityReferenceNumber;
            existing.SubmissionMethod        = model.SubmissionMethod;
            existing.IsOrphanDesignation     = model.IsOrphanDesignation;
            existing.IsPriorityReview        = model.IsPriorityReview;
            existing.IsBreakthroughTherapy   = model.IsBreakthroughTherapy;
            existing.IsPRIME                 = model.IsPRIME;
            existing.IsConditionalApproval   = model.IsConditionalApproval;
            existing.StrategyStartDate       = model.StrategyStartDate;
            existing.PreSubMeetingDate       = model.PreSubMeetingDate;
            existing.DossierLockDate         = model.DossierLockDate;
            existing.SubmissionDate          = model.SubmissionDate;
            existing.AcknowledgementDate     = model.AcknowledgementDate;
            existing.Day0Date                = model.Day0Date;
            existing.TargetDecisionDate      = model.TargetDecisionDate;
            existing.ActualDecisionDate      = model.ActualDecisionDate;
            existing.ApprovalDate            = model.ApprovalDate;
            existing.ExpiryDate              = model.ExpiryDate;
            existing.NextRenewalDue          = model.NextRenewalDue;
            existing.AppealDeadline          = model.AppealDeadline;
            existing.ApplicationFee          = model.ApplicationFee;
            existing.FeeCurrency             = model.FeeCurrency;
            existing.FeePaidDate             = model.FeePaidDate;
            existing.FeeWaiverApplied        = model.FeeWaiverApplied;
            existing.FeeWaiverReason         = model.FeeWaiverReason;
            existing.InternalRALead          = model.InternalRALead;
            existing.ExternalConsultant      = model.ExternalConsultant;
            existing.AuthorityRapporteur     = model.AuthorityRapporteur;
            existing.AuthorityContactEmail   = model.AuthorityContactEmail;
            existing.ManufacturingSupplierId = model.ManufacturingSupplierId;
            existing.StrategyNotes           = model.StrategyNotes;
            existing.ModifiedBy              = user?.UserName ?? User.Identity?.Name;
            existing.ModifiedAt              = DateTime.UtcNow;

            _context.RIAuditLogs.AddRange(auditEntries);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.RegulatoryApplications.Any(x => x.Id == id)) return NotFound();
                throw;
            }

            TempData["Success"] = $"Application {existing.RINumber} updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ────────────────────────────────────────────────────────────

        // POST /RegulatoryIntelligence/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var app = await _context.RegulatoryApplications.FindAsync(id);
            if (app == null) return NotFound();

            app.IsDeleted  = true;
            app.ModifiedBy = User.Identity?.Name;
            app.ModifiedAt = DateTime.UtcNow;

            var user = await _userManager.GetUserAsync(User);
            _context.RIAuditLogs.Add(new RIAuditLog
            {
                RegulatoryApplicationId = id,
                Action = "Deleted",
                OldValue = app.RINumber,
                UserId  = user?.UserName ?? User.Identity?.Name ?? "Unknown",
                Notes   = "Soft-deleted."
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Application {app.RINumber} deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── AJAX: CASCADE DROPDOWN ────────────────────────────────────────────

        // GET /RegulatoryIntelligence/GetApplicationTypes?jurisdictionId=X
        [HttpGet]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> GetApplicationTypes(int jurisdictionId)
        {
            var types = await _context.RIApplicationTypes
                .Where(t => t.JurisdictionId == jurisdictionId && t.IsActive)
                .OrderBy(t => t.SortOrder)
                .Select(t => new { t.Id, t.Code, t.Name, t.ClockDays, t.RequiresApproval })
                .ToListAsync();
            return Json(types);
        }

        // GET /RegulatoryIntelligence/GetProductDetails?productId=X
        [HttpGet]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            var product = await _context.Product
                .Include(p => p.Strengths)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);

            if (product == null) return Json(null);

            var strengths = product.Strengths?
                .Select(s => s.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList() ?? new List<string>();

            return Json(new {
                name       = product.Name,
                brandName  = product.BrandName,
                innName    = product.InnName,
                dosageForm = product.DosageForm,
                strengths  = strengths.Any() ? string.Join(" / ", strengths) : (string?)null
            });
        }

        // GET /RegulatoryIntelligence/CalculateTargetDate?day0=2026-01-01&appTypeId=5
        [HttpGet]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CalculateTargetDate(string day0, int appTypeId)
        {
            if (!DateTime.TryParse(day0, out var day0Date))
                return Json(new { targetDate = (string?)null });

            var appType = await _context.RIApplicationTypes.FindAsync(appTypeId);
            if (appType?.ClockDays == null)
                return Json(new { targetDate = (string?)null });

            // Add calendar days as an approximation of working days for display
            var target = day0Date.AddDays(appType.ClockDays.Value);
            return Json(new { targetDate = target.ToString("yyyy-MM-dd") });
        }

        // ── DOCUMENTS ─────────────────────────────────────────────────────────

        // POST /RegulatoryIntelligence/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UploadDocument(int applicationId, IFormFile file, string? documentType)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, Excel, ZIP, JPG, PNG.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File size exceeds the 25 MB limit.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var app = await _context.RegulatoryApplications.FindAsync(applicationId);
            if (app == null) return NotFound();

            if (app.IsLocked)
            {
                TempData["Error"] = "This record is locked (finalised) and cannot be modified — documents cannot be added.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "regulatory", applicationId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath       = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var user = await _userManager.GetUserAsync(User);
            var doc  = new RIDocument
            {
                RegulatoryApplicationId = applicationId,
                FileName     = file.FileName,
                FilePath     = $"/uploads/regulatory/{applicationId}/{uniqueFileName}",
                DocumentType = documentType,
                FileSizeBytes = file.Length,
                FileHash     = fileHash,
                Version      = 1,
                UploadedBy   = user?.UserName ?? User.Identity?.Name ?? "Unknown",
                UploadedAt   = DateTime.UtcNow
            };

            _context.RIDocuments.Add(doc);

            _context.RIAuditLogs.Add(new RIAuditLog
            {
                RegulatoryApplicationId = applicationId,
                Action  = "DocumentUploaded",
                NewValue = file.FileName,
                UserId  = user?.UserName ?? User.Identity?.Name ?? "Unknown",
                Notes   = $"Uploaded {documentType ?? "document"}: {file.FileName}"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Document '{file.FileName}' uploaded.";
            TempData["ActiveTab"] = "dtab7";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST /RegulatoryIntelligence/DeleteDocument/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteDocument(int id, string? deletionReason)
        {
            var doc = await _context.RIDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            var applicationId = doc.RegulatoryApplicationId;

            var app = await _context.RegulatoryApplications.FindAsync(applicationId);
            if (app != null && app.IsLocked)
            {
                TempData["Error"] = "This record is locked (finalised) and cannot be modified — documents cannot be removed.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var user = await _userManager.GetUserAsync(User);

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = user?.UserName ?? User.Identity?.Name ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();

            _context.RIAuditLogs.Add(new RIAuditLog
            {
                RegulatoryApplicationId = applicationId,
                Action  = "DocumentDeleted",
                OldValue = doc.FileName,
                NewValue = $"Reason: {doc.DeletionReason}",
                UserId  = user?.UserName ?? User.Identity?.Name ?? "Unknown"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Document '{doc.FileName}' deleted.";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // GET /RegulatoryIntelligence/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.RIDocuments.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (doc == null) return NotFound();

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(fullPath, doc.FileHash))
            {
                _context.RIAuditLogs.Add(new RIAuditLog { RegulatoryApplicationId = doc.RegulatoryApplicationId, Action = "IntegrityCheckFailed", OldValue = doc.FileName, NewValue = OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName), UserId = User.Identity?.Name ?? "Unknown" });
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction(nameof(Details), new { id = doc.RegulatoryApplicationId });
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.RegulatoryAttachment, null, "Regulatory Intelligence",
                doc.RegulatoryApplicationId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, "application/octet-stream", doc.FileName);
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private string GenerateRINumber()
        {
            var year = DateTime.UtcNow.Year;
            var last = _context.RegulatoryApplications
                .Where(x => x.RINumber.StartsWith($"RI-{year}-"))
                .OrderByDescending(x => x.Id)
                .Select(x => x.RINumber)
                .FirstOrDefault();

            int seq = 1;
            if (last != null)
            {
                var parts = last.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int n)) seq = n + 1;
            }
            return $"RI-{year}-{seq:D4}";
        }

        private async Task PopulateCreateEditViewData(
            int? selectedJurisdictionId = null,
            int? selectedApplicationTypeId = null,
            int? selectedSupplierId = null,
            int? selectedProductId = null,
            string? selectedRALeadId = null)
        {
            // Products — only the current active (Approved) version of each product.
            // Keep the currently-selected product even if it has since been superseded
            // so an existing record's selection is never silently lost on edit.
            ViewData["Products"] = new SelectList(
                _context.Product
                    .Where(x => !x.IsDeleted
                             && (x.Status == Product.ProductStatus.Approved || x.Id == selectedProductId))
                    .OrderBy(x => x.Name).ToList(),
                "Id", "Name", selectedProductId);

            // Internal RA Lead — only users with regulatory module access (mirrors LC pattern).
            var regulatoryRoles = new[] { "Administrator", "RegulatoryManager", "RegulatoryUser" };
            var regulatoryUsers = new List<AppUser>();
            foreach (var role in regulatoryRoles)
                regulatoryUsers.AddRange(await _userManager.GetUsersInRoleAsync(role));

            var raUsers = regulatoryUsers
                .DistinctBy(u => u.Id)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .ToList();

            ViewBag.UsersList = new SelectList(
                raUsers.Select(u => new { Id = u.Id.ToString(), Name = $"{u.FirstName} {u.LastName} ({u.UserName})".Trim() }),
                "Id", "Name",
                selectedRALeadId);

            ViewData["Jurisdictions"] = new SelectList(
                _context.RIJurisdictions.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToList(),
                "Id", "Name",
                selectedJurisdictionId);

            // Application types pre-load for the selected jurisdiction (or empty)
            var appTypes = selectedJurisdictionId.HasValue
                ? _context.RIApplicationTypes
                    .Where(t => t.JurisdictionId == selectedJurisdictionId && t.IsActive)
                    .OrderBy(t => t.SortOrder)
                    .ToList()
                : new List<RIApplicationType>();

            ViewData["ApplicationTypes"] = new SelectList(appTypes, "Id", "Name", selectedApplicationTypeId);

            ViewData["Suppliers"] = new SelectList(
                _context.Supplier.Where(x => !x.IsDeleted).OrderBy(x => x.LegalName).ToList(),
                "Id", "LegalName",
                selectedSupplierId);

            ViewData["DocumentTypes"] = new SelectList(new[]
            {
                "eCTD Module", "Approval Letter", "PI (Product Information)",
                "CMI (Consumer Medicine Information)", "SmPC", "REMS", "RMP",
                "Condition Letter", "Assessment Report", "Labelling", "Other"
            });
        }

        private List<RIAuditLog> BuildAuditDiff(
            RegulatoryApplication existing,
            RegulatoryApplication updated,
            string userId)
        {
            var entries = new List<RIAuditLog>();

            void Check(string field, object? oldVal, object? newVal)
            {
                var o = oldVal?.ToString() ?? "";
                var n = newVal?.ToString() ?? "";
                if (o != n)
                {
                    entries.Add(new RIAuditLog
                    {
                        RegulatoryApplicationId = existing.Id,
                        Action       = "FieldEdited",
                        FieldChanged = field,
                        OldValue     = o,
                        NewValue     = n,
                        UserId       = userId
                    });
                }
            }

            Check("ProductId",               existing.ProductId,               updated.ProductId);
            Check("JurisdictionId",          existing.JurisdictionId,          updated.JurisdictionId);
            Check("ApplicationTypeId",       existing.ApplicationTypeId,       updated.ApplicationTypeId);
            Check("Status",                  existing.Status,                  updated.Status);
            Check("SubmissionDate",          existing.SubmissionDate,          updated.SubmissionDate);
            Check("Day0Date",                existing.Day0Date,                updated.Day0Date);
            Check("TargetDecisionDate",      existing.TargetDecisionDate,      updated.TargetDecisionDate);
            Check("ApprovalDate",            existing.ApprovalDate,            updated.ApprovalDate);
            Check("ExpiryDate",              existing.ExpiryDate,              updated.ExpiryDate);
            Check("AuthorityReferenceNumber",existing.AuthorityReferenceNumber,updated.AuthorityReferenceNumber);
            Check("InternalRALead",          existing.InternalRALead,          updated.InternalRALead);

            return entries;
        }

        // ── WORKFLOW ──────────────────────────────────────────────────────────

        // POST /RegulatoryIntelligence/Transition — non-password-gated transitions
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Transition(int id, RIApplicationStatus newStatus)
        {
            var app = await _context.RegulatoryApplications
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (app == null) return NotFound();

            var result = _workflow.ValidateTransition(app, newStatus);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            var performedBy   = _userManager.GetUserName(User) ?? User.Identity?.Name ?? "Unknown";
            var performedById = _userManager.GetUserId(User) ?? performedBy;
            await _workflow.ApplyTransition(app, newStatus, performedBy, performedByUserId: performedById);

            TempData["Success"] = $"Status updated to: {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /RegulatoryIntelligence/InitiateRenewal — early / unscheduled renewal
        // Moves an Approved application to Renewal Due ahead of the expiry-driven
        // reminders, capturing a justification for the audit trail.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> InitiateRenewal(int id, string reason)
        {
            var app = await _context.RegulatoryApplications
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (app == null) return NotFound();

            if (app.Status != RIApplicationStatus.Approved)
            {
                TempData["Error"] = "A renewal can only be initiated from an Approved application.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A reason is required to initiate an early renewal.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var note   = $"Early renewal initiated: {reason.Trim()}";
            var result = _workflow.ValidateTransition(app, RIApplicationStatus.RenewalDue, note);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            var performedBy   = _userManager.GetUserName(User) ?? User.Identity?.Name ?? "Unknown";
            var performedById = _userManager.GetUserId(User) ?? performedBy;
            await _workflow.ApplyTransition(app, RIApplicationStatus.RenewalDue, performedBy, note, performedByUserId: performedById);

            TempData["Success"] = "Renewal initiated — application moved to Renewal Due.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /RegulatoryIntelligence/ApproveApplication — password-gated
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> ApproveApplication(int id, string email, string pwd)
        {
            // If conditional approval flag is set, transition to ConditionsActive instead
            var app = await _context.RegulatoryApplications
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (app == null) return NotFound();

            var targetStatus = app.IsConditionalApproval
                ? RIApplicationStatus.ConditionsActive
                : RIApplicationStatus.Approved;

            return await TransitionWithCredentials(id, email, pwd, targetStatus, null);
        }

        // POST /RegulatoryIntelligence/RejectApplication — password-gated, reason required
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> RejectApplication(int id, string email, string pwd, string reason)
            => await TransitionWithCredentials(id, email, pwd, RIApplicationStatus.Rejected, reason);

        // POST /RegulatoryIntelligence/WithdrawApplication — password-gated, reason required
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> WithdrawApplication(int id, string email, string pwd, string reason)
            => await TransitionWithCredentials(id, email, pwd, RIApplicationStatus.Withdrawn, reason);

        // POST /RegulatoryIntelligence/SuspendApplication — password-gated, reason required
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> SuspendApplication(int id, string email, string pwd, string reason)
            => await TransitionWithCredentials(id, email, pwd, RIApplicationStatus.Suspended, reason);

        private async Task<IActionResult> TransitionWithCredentials(
            int id, string email, string pwd,
            RIApplicationStatus newStatus,
            string? reason)
        {
            // Verify credentials
            // E-signature must be made with the signed-in user's own credentials
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user = null;
            if (user == null ||
                _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, pwd) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Invalid credentials. Action not authorised.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var app = await _context.RegulatoryApplications
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (app == null) return NotFound();

            var result = _workflow.ValidateTransition(app, newStatus, reason);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            var performedBy = _userManager.GetUserName(User) ?? email;
            await _workflow.ApplyTransition(app, newStatus, performedBy, reason, performedByUserId: user.Id.ToString());

            TempData["Success"] = $"Status updated to: {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── CONDITIONS OF APPROVAL ───────────────────────────────────────────

        // POST /RegulatoryIntelligence/AddCondition
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> AddCondition(
            int applicationId, string conditionDescription,
            RIConditionType conditionType, DateTime? dueDate)
        {
            var app = await _context.RegulatoryApplications.FindAsync(applicationId);
            if (app == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            _context.RIConditionsOfApproval.Add(new RIConditionOfApproval
            {
                RegulatoryApplicationId = applicationId,
                ConditionDescription    = conditionDescription,
                ConditionType           = conditionType,
                DueDate                 = dueDate,
                Status                  = RIConditionStatus.Outstanding,
                CreatedBy               = user?.UserName ?? User.Identity?.Name ?? "Unknown"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Condition of approval added.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST /RegulatoryIntelligence/UpdateCondition
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UpdateCondition(
            int id, string conditionDescription,
            RIConditionType conditionType, DateTime? dueDate,
            RIConditionStatus status)
        {
            var condition = await _context.RIConditionsOfApproval.FindAsync(id);
            if (condition == null) return NotFound();

            condition.ConditionDescription = conditionDescription;
            condition.ConditionType        = conditionType;
            condition.DueDate              = dueDate;
            condition.Status               = status;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Condition updated.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = condition.RegulatoryApplicationId });
        }

        // POST /RegulatoryIntelligence/CloseCondition
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CloseCondition(int id, string? completionNotes)
        {
            var condition = await _context.RIConditionsOfApproval.FindAsync(id);
            if (condition == null) return NotFound();

            condition.Status          = RIConditionStatus.Closed;
            condition.CompletedDate   = SiteTime.Today;
            condition.CompletionNotes = completionNotes;
            await _context.SaveChangesAsync();

            // Auto-transition: ConditionsActive → Approved when all conditions closed/waived
            var app = await _context.RegulatoryApplications
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == condition.RegulatoryApplicationId);

            if (app != null && app.Status == RIApplicationStatus.ConditionsActive)
            {
                var openCount = app.Conditions.Count(
                    c => c.Status != RIConditionStatus.Closed && c.Status != RIConditionStatus.Waived);

                if (openCount == 0)
                {
                    var performedBy   = _userManager.GetUserName(User) ?? User.Identity?.Name ?? "System";
                    var performedById = _userManager.GetUserId(User) ?? performedBy;
                    await _workflow.ApplyTransition(
                        app, RIApplicationStatus.Approved, performedBy,
                        "All conditions of approval closed — auto-transitioned to Approved.",
                        performedByUserId: performedById);

                    TempData["Success"] = "Condition closed. All conditions satisfied — application transitioned to Approved.";
                    TempData["ActiveTab"] = "dtab6";
                    return RedirectToAction(nameof(Details), new { id = condition.RegulatoryApplicationId });
                }
            }

            TempData["Success"] = "Condition closed.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = condition.RegulatoryApplicationId });
        }

        // POST /RegulatoryIntelligence/DeleteCondition
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteCondition(int id)
        {
            var condition = await _context.RIConditionsOfApproval.FindAsync(id);
            if (condition == null) return NotFound();

            var applicationId = condition.RegulatoryApplicationId;
            _context.RIConditionsOfApproval.Remove(condition);
            await _context.SaveChangesAsync();

            // Auto-transition: ConditionsActive → Approved when all remaining conditions closed/waived
            var app = await _context.RegulatoryApplications
                .Include(x => x.Conditions)
                .FirstOrDefaultAsync(x => x.Id == applicationId);

            if (app != null && app.Status == RIApplicationStatus.ConditionsActive)
            {
                var openCount = app.Conditions.Count(
                    c => c.Status != RIConditionStatus.Closed && c.Status != RIConditionStatus.Waived);

                if (openCount == 0)
                {
                    var performedBy   = _userManager.GetUserName(User) ?? User.Identity?.Name ?? "System";
                    var performedById = _userManager.GetUserId(User) ?? performedBy;
                    await _workflow.ApplyTransition(
                        app, RIApplicationStatus.Approved, performedBy,
                        "All conditions of approval resolved — auto-transitioned to Approved.",
                        performedByUserId: performedById);

                    TempData["Success"] = "Condition deleted. All conditions satisfied — application transitioned to Approved.";
                    TempData["ActiveTab"] = "dtab6";
                    return RedirectToAction(nameof(Details), new { id = applicationId });
                }
            }

            TempData["Success"] = "Condition deleted.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST /RegulatoryIntelligence/CreateCAPAFromCondition
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CreateCAPAFromCondition(int conditionId)
        {
            var condition = await _context.RIConditionsOfApproval
                .Include(x => x.Application)
                .FirstOrDefaultAsync(x => x.Id == conditionId);
            if (condition == null) return NotFound();

            // Generate next CAPA ID — match CapasController: 4-digit, max-numeric (not lexicographic)
            var allCapaIds = await _context.Capa
                .Where(c => c.CapaId != null)
                .Select(c => c.CapaId!)
                .ToListAsync();
            int lastId = allCapaIds
                .Select(cid => { var p = cid.Split('-'); return p.Length == 2 && int.TryParse(p[1], out int n) ? n : 0; })
                .DefaultIfEmpty(0)
                .Max();
            var capaId = $"CPA-{(lastId + 1).ToString().PadLeft(4, '0')}";
            var user     = await _userManager.GetUserAsync(User);
            var userName = user?.UserName ?? User.Identity?.Name ?? "Unknown";

            var capa = new Capa
            {
                CapaId               = capaId,
                Title                = $"Condition of Approval: {condition.ConditionDescription}",
                CorrectiveAction     = condition.ConditionDescription,
                PreventiveAction     = $"Regulatory condition imposed for {condition.Application?.RINumber ?? "RI application"}",
                TargetCompletionDate = condition.DueDate,
                DeterminedBy         = userName,
                DeterminedOn         = SiteTime.Today,
                Status               = Capa.CapaStatus.Determination
            };

            _context.Capa.Add(capa);
            await _context.SaveChangesAsync();

            condition.LinkedCAPAId = capa.Id;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"CAPA {capaId} created and linked to this condition.";
            return RedirectToAction(nameof(Details), new { id = condition.RegulatoryApplicationId });
        }

        // ── MILESTONES ────────────────────────────────────────────────────────

        // POST /RegulatoryIntelligence/AddMilestone
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> AddMilestone(
            int applicationId, string milestoneName,
            DateTime? plannedDate, bool isClockStop, string? notes)
        {
            var app = await _context.RegulatoryApplications.FindAsync(applicationId);
            if (app == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            _context.RIMilestones.Add(new RIMilestone
            {
                RegulatoryApplicationId = applicationId,
                MilestoneName           = milestoneName,
                PlannedDate             = plannedDate,
                IsClockStop             = isClockStop,
                Notes                   = notes,
                CreatedBy               = user?.UserName ?? User.Identity?.Name ?? "Unknown"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Milestone '{milestoneName}' added.";
            TempData["ActiveTab"] = "dtabMilestones";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST /RegulatoryIntelligence/UpdateMilestone
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UpdateMilestone(
            int id, string milestoneName,
            DateTime? plannedDate, DateTime? actualDate,
            RIMilestoneStatus status,
            bool isClockStop, DateTime? clockRestartDate, string? notes)
        {
            var milestone = await _context.RIMilestones.FindAsync(id);
            if (milestone == null) return NotFound();

            milestone.MilestoneName    = milestoneName;
            milestone.PlannedDate      = plannedDate;
            milestone.ActualDate       = actualDate;
            milestone.Status           = status;
            milestone.IsClockStop      = isClockStop;
            milestone.ClockRestartDate = clockRestartDate;
            milestone.Notes            = notes;

            // Auto-detect overdue
            if (milestone.Status == RIMilestoneStatus.Pending &&
                milestone.PlannedDate.HasValue && milestone.PlannedDate < SiteTime.Today)
                milestone.Status = RIMilestoneStatus.Overdue;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Milestone '{milestoneName}' updated.";
            TempData["ActiveTab"] = "dtabMilestones";
            return RedirectToAction(nameof(Details), new { id = milestone.RegulatoryApplicationId });
        }

        // POST /RegulatoryIntelligence/DeleteMilestone
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteMilestone(int id)
        {
            var milestone = await _context.RIMilestones.FindAsync(id);
            if (milestone == null) return NotFound();

            var applicationId = milestone.RegulatoryApplicationId;
            _context.RIMilestones.Remove(milestone);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Milestone deleted.";
            TempData["ActiveTab"] = "dtabMilestones";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST /RegulatoryIntelligence/ApplyMilestoneTemplate
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> ApplyMilestoneTemplate(int applicationId)
        {
            var app = await _context.RegulatoryApplications
                .Include(x => x.Jurisdiction)
                .Include(x => x.Milestones)
                .FirstOrDefaultAsync(x => x.Id == applicationId);
            if (app == null) return NotFound();

            if (app.Milestones.Any())
            {
                TempData["Error"] = "Milestones already exist. Delete all milestones before applying a template.";
                TempData["ActiveTab"] = "dtabMilestones";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var templates = GetMilestoneTemplates(app.Jurisdiction?.Code ?? "");
            if (!templates.Any())
            {
                TempData["Error"] = $"No standard template available for {app.Jurisdiction?.Authority ?? "this jurisdiction"}.";
                TempData["ActiveTab"] = "dtabMilestones";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var user      = await _userManager.GetUserAsync(User);
            var createdBy = user?.UserName ?? User.Identity?.Name ?? "Unknown";
            var baseDate  = app.Day0Date ?? app.SubmissionDate ?? SiteTime.Today;

            foreach (var (name, offsetDays, isClockStop) in templates)
            {
                _context.RIMilestones.Add(new RIMilestone
                {
                    RegulatoryApplicationId = applicationId,
                    MilestoneName           = name,
                    PlannedDate             = baseDate.AddDays(offsetDays),
                    IsClockStop             = isClockStop,
                    CreatedBy               = createdBy
                });
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Applied {templates.Count} standard milestones for {app.Jurisdiction?.Authority}.";
            TempData["ActiveTab"] = "dtabMilestones";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // ── ACCESS PATHWAYS ───────────────────────────────────────────────────

        // GET /RegulatoryIntelligence/CreateAccessPathway?applicationId=5
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CreateAccessPathway(int applicationId)
        {
            var app = await _context.RegulatoryApplications
                .Include(x => x.Jurisdiction)
                .FirstOrDefaultAsync(x => x.Id == applicationId && !x.IsDeleted);
            if (app == null) return NotFound();

            ViewData["ApplicationId"] = applicationId;
            ViewData["RINumber"]      = app.RINumber;
            ViewData["Authority"]     = app.Jurisdiction?.Authority;

            return View("AccessPathwayCreate", new RIAccessPathwayRecord
            {
                RegulatoryApplicationId = applicationId,
                AccessNumber            = GenerateAPNumber()
            });
        }

        // POST /RegulatoryIntelligence/CreateAccessPathway
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CreateAccessPathway(RIAccessPathwayRecord model)
        {
            ModelState.Remove("Application");
            ModelState.Remove("SupplyReports");
            ModelState.Remove("CreatedBy");

            if (!ModelState.IsValid)
            {
                ViewData["ApplicationId"] = model.RegulatoryApplicationId;
                return View("AccessPathwayCreate", model);
            }

            var user = await _userManager.GetUserAsync(User);
            model.CreatedBy = user?.UserName ?? User.Identity?.Name ?? "Unknown";
            model.CreatedAt = DateTime.UtcNow;

            _context.RIAccessPathwayRecords.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Access pathway {model.AccessNumber} created.";
            return RedirectToAction(nameof(AccessPathwayDetails), new { id = model.Id });
        }

        // GET /RegulatoryIntelligence/EditAccessPathway/5
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> EditAccessPathway(int id)
        {
            var ap = await _context.RIAccessPathwayRecords
                .Include(x => x.Application)
                .ThenInclude(x => x.Jurisdiction)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (ap == null) return NotFound();

            ViewData["ApplicationId"] = ap.RegulatoryApplicationId;
            ViewData["RINumber"]      = ap.Application?.RINumber;
            ViewData["Authority"]     = ap.Application?.Jurisdiction?.Authority;

            return View("AccessPathwayEdit", ap);
        }

        // POST /RegulatoryIntelligence/EditAccessPathway
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> EditAccessPathway(RIAccessPathwayRecord model)
        {
            ModelState.Remove("Application");
            ModelState.Remove("SupplyReports");
            ModelState.Remove("CreatedBy");

            if (!ModelState.IsValid)
            {
                ViewData["ApplicationId"] = model.RegulatoryApplicationId;
                return View("AccessPathwayEdit", model);
            }

            var existing = await _context.RIAccessPathwayRecords.FindAsync(model.Id);
            if (existing == null) return NotFound();

            existing.AccessNumber            = model.AccessNumber;
            existing.PathwayType             = model.PathwayType;
            existing.PatientIdentifier       = model.PatientIdentifier;
            existing.PrescriberId            = model.PrescriberId;
            existing.PrescriberName          = model.PrescriberName;
            existing.TGAApprovalLetterNumber = model.TGAApprovalLetterNumber;
            existing.ApprovalDate            = model.ApprovalDate;
            existing.ExpiryDate              = model.ExpiryDate;
            existing.Status                  = model.Status;
            existing.NextSixMonthlyReportDue = model.NextSixMonthlyReportDue;
            existing.AEReportingRequired     = model.AEReportingRequired;
            existing.ClinicalJustification   = model.ClinicalJustification;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Access pathway {existing.AccessNumber} updated.";
            return RedirectToAction(nameof(AccessPathwayDetails), new { id = existing.Id });
        }

        // POST /RegulatoryIntelligence/DeleteAccessPathway
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteAccessPathway(int id)
        {
            var ap = await _context.RIAccessPathwayRecords.FindAsync(id);
            if (ap == null) return NotFound();

            var applicationId = ap.RegulatoryApplicationId;
            _context.RIAccessPathwayRecords.Remove(ap);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Access pathway deleted.";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // GET /RegulatoryIntelligence/AccessPathwayDetails/5
        public async Task<IActionResult> AccessPathwayDetails(int id)
        {
            var ap = await _context.RIAccessPathwayRecords
                .Include(x => x.Application)
                .ThenInclude(x => x.Jurisdiction)
                .Include(x => x.SupplyReports)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (ap == null) return NotFound();

            var linkedAECount = await _context.AdverseEvent
                .CountAsync(ae => ae.AccessPathwayRecordId == id);
            ViewData["LinkedAECount"] = linkedAECount;

            return View(ap);
        }

        // ── SIX-MONTHLY SUPPLY REPORTS ────────────────────────────────────────

        // POST /RegulatoryIntelligence/AddSixMonthlyReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> AddSixMonthlyReport(
            int accessPathwayId,
            DateTime periodStart, DateTime periodEnd,
            int quantitySupplied, string? unit,
            DateTime? submittedToAuthorityDate, string? authorityReferenceNumber,
            RIReportStatus status, string? notes)
        {
            var ap = await _context.RIAccessPathwayRecords.FindAsync(accessPathwayId);
            if (ap == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            _context.RISixMonthlySupplyReports.Add(new RISixMonthlySupplyReport
            {
                AccessPathwayRecordId    = accessPathwayId,
                PeriodStart              = periodStart,
                PeriodEnd                = periodEnd,
                QuantitySupplied         = quantitySupplied,
                Unit                     = unit,
                SubmittedToAuthorityDate = submittedToAuthorityDate,
                AuthorityReferenceNumber = authorityReferenceNumber,
                Status                   = status,
                Notes                    = notes,
                CreatedBy                = user?.UserName ?? User.Identity?.Name ?? "Unknown"
            });

            // Auto-advance next report due date
            ap.NextSixMonthlyReportDue = periodEnd.AddMonths(6);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Six-monthly supply report added.";
            return RedirectToAction(nameof(AccessPathwayDetails), new { id = accessPathwayId });
        }

        // POST /RegulatoryIntelligence/UpdateSixMonthlyReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UpdateSixMonthlyReport(
            int id,
            DateTime periodStart, DateTime periodEnd,
            int quantitySupplied, string? unit,
            DateTime? submittedToAuthorityDate, string? authorityReferenceNumber,
            RIReportStatus status, string? notes)
        {
            var report = await _context.RISixMonthlySupplyReports.FindAsync(id);
            if (report == null) return NotFound();

            report.PeriodStart              = periodStart;
            report.PeriodEnd                = periodEnd;
            report.QuantitySupplied         = quantitySupplied;
            report.Unit                     = unit;
            report.SubmittedToAuthorityDate = submittedToAuthorityDate;
            report.AuthorityReferenceNumber = authorityReferenceNumber;
            report.Status                   = status;
            report.Notes                    = notes;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Supply report updated.";
            return RedirectToAction(nameof(AccessPathwayDetails), new { id = report.AccessPathwayRecordId });
        }

        // POST /RegulatoryIntelligence/DeleteSixMonthlyReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteSixMonthlyReport(int id)
        {
            var report = await _context.RISixMonthlySupplyReports.FindAsync(id);
            if (report == null) return NotFound();

            var apId = report.AccessPathwayRecordId;
            _context.RISixMonthlySupplyReports.Remove(report);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Supply report deleted.";
            return RedirectToAction(nameof(AccessPathwayDetails), new { id = apId });
        }

        private string GenerateAPNumber()
        {
            var year = DateTime.UtcNow.Year;
            var last = _context.RIAccessPathwayRecords
                .Where(x => x.AccessNumber.StartsWith($"AP-{year}-"))
                .OrderByDescending(x => x.Id)
                .Select(x => x.AccessNumber)
                .FirstOrDefault();

            int seq = 1;
            if (last != null)
            {
                var parts = last.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int n)) seq = n + 1;
            }
            return $"AP-{year}-{seq:D4}";
        }

        // ── VARIATIONS ───────────────────────────────────────────────────────

        // POST /RegulatoryIntelligence/AddVariation
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> AddVariation(
            int applicationId, string variationDescription,
            RIVariationType variationType, DateTime? submissionDate, string? notes)
        {
            var app = await _context.RegulatoryApplications.FindAsync(applicationId);
            if (app == null) return NotFound();

            // Variations are post-approval changes — reject if no authorisation exists yet.
            var canVariate = app.Status is RIApplicationStatus.Approved
                                        or RIApplicationStatus.ConditionsActive
                                        or RIApplicationStatus.VariationRequired
                                        or RIApplicationStatus.RenewalDue;
            if (!canVariate)
            {
                TempData["Error"] = "Variations can only be raised once the application is approved.";
                TempData["ActiveTab"] = "dtabVariations";
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var user     = await _userManager.GetUserAsync(User);
            var varNumber = GenerateVariationNumber(app.RINumber);

            _context.RIVariations.Add(new RIVariation
            {
                ParentApplicationId  = applicationId,
                VariationNumber      = varNumber,
                VariationDescription = variationDescription,
                VariationType        = variationType,
                Status               = RIApplicationStatus.Preparation,
                SubmissionDate       = submissionDate,
                Notes                = notes,
                CreatedBy            = user?.UserName ?? User.Identity?.Name ?? "Unknown"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Variation {varNumber} added.";
            TempData["ActiveTab"] = "dtabVariations";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST /RegulatoryIntelligence/UpdateVariation
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UpdateVariation(
            int id, string variationDescription,
            RIVariationType variationType, RIApplicationStatus status,
            DateTime? submissionDate, DateTime? approvalDate, string? notes)
        {
            var variation = await _context.RIVariations.FindAsync(id);
            if (variation == null) return NotFound();

            variation.VariationDescription = variationDescription;
            variation.VariationType        = variationType;
            variation.Status               = status;
            variation.SubmissionDate       = submissionDate;
            variation.ApprovalDate         = approvalDate;
            variation.Notes                = notes;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Variation {variation.VariationNumber} updated.";
            TempData["ActiveTab"] = "dtabVariations";
            return RedirectToAction(nameof(Details), new { id = variation.ParentApplicationId });
        }

        // POST /RegulatoryIntelligence/DeleteVariation
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteVariation(int id)
        {
            var variation = await _context.RIVariations.FindAsync(id);
            if (variation == null) return NotFound();

            var applicationId = variation.ParentApplicationId;
            _context.RIVariations.Remove(variation);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Variation deleted.";
            TempData["ActiveTab"] = "dtabVariations";
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        private string GenerateVariationNumber(string riNumber)
        {
            var count = _context.RIVariations
                .Count(v => v.VariationNumber.StartsWith(riNumber + "-V"));
            return $"{riNumber}-V{(count + 1):D2}";
        }

        private static List<(string Name, int OffsetDays, bool IsClockStop)> GetMilestoneTemplates(string jurisdictionCode)
        {
            return jurisdictionCode switch
            {
                "AU" => new()
                {
                    ("Submission Lodged",                       0,   false),
                    ("Submission Acknowledgement",             15,   false),
                    ("Preliminary Assessment Complete",        30,   false),
                    ("Day 45 First Round Questions Issued",    45,   true),
                    ("Day 45 Clock-Stop Ends (Response Due)", 135,   false),
                    ("Day 150 Second Round Assessment",       150,   false),
                    ("Advisory Committee (if required)",      200,   false),
                    ("Draft Decision Issued",                 225,   false),
                    ("Final Decision",                        255,   false),
                },
                "NZ" => new()
                {
                    ("Submission Lodged",                       0,   false),
                    ("Validation Complete",                    10,   false),
                    ("Screening Assessment Complete",          30,   false),
                    ("Preliminary Questions (if any)",         60,   true),
                    ("Primary Evaluation Complete",           130,   false),
                    ("Second Evaluation Complete",            165,   false),
                    ("Delegate Decision",                     185,   false),
                },
                "EU" => new()
                {
                    ("Submission / Day 0",                      0,   false),
                    ("Day 70 CHMP Assessment Report",          70,   false),
                    ("Day 120 List of Questions",             120,   true),
                    ("Day 150 Applicant Responses Submitted", 150,   false),
                    ("Day 180 Assessment of Responses",       180,   false),
                    ("Day 210 List of Outstanding Issues",    210,   true),
                    ("Day 240 Applicant Responses (OI)",      240,   false),
                    ("Day 270 CHMP Opinion",                  270,   false),
                    ("Day 277 European Commission Decision",  277,   false),
                },
                "US" => new()
                {
                    ("Submission / Receipt",                    0,   false),
                    ("Filing Review Complete",                 60,   false),
                    ("Mid-Cycle Communication",                90,   false),
                    ("Day 100 Information Request",           100,   true),
                    ("Labelling Negotiations",                170,   false),
                    ("PDUFA Goal Date",                       365,   false),
                    ("Final Action (Approval/CRL)",           365,   false),
                },
                "CA" => new()
                {
                    ("Submission Received",                     0,   false),
                    ("Screening Review Complete",              45,   false),
                    ("Preliminary Assessment",                 90,   false),
                    ("Screening Deficiency Notice (if any)",   90,   true),
                    ("Detailed Review Complete",              180,   false),
                    ("Notice of Decision",                    300,   false),
                },
                "SG" => new()
                {
                    ("Submission Received",                     0,   false),
                    ("Screening Outcome",                      30,   false),
                    ("Evaluation Phase 1 Complete",            90,   false),
                    ("Query Letter (if any)",                 120,   true),
                    ("Evaluation Phase 2 Complete",           270,   false),
                    ("Approval / Rejection",                  370,   false),
                },
                "MY" => new()
                {
                    ("Submission Received",                     0,   false),
                    ("Screening Complete",                     30,   false),
                    ("Phase 1 Evaluation",                    120,   false),
                    ("Phase 1 Query (if any)",                120,   true),
                    ("Phase 2 Evaluation",                    240,   false),
                    ("Approval",                              365,   false),
                },
                "BR" => new()
                {
                    ("Submission Filed",                        0,   false),
                    ("Technical Filing Complete",              30,   false),
                    ("Administrative Review",                  60,   false),
                    ("Technical Analysis Phase 1",            180,   false),
                    ("Technical Query (if any)",              180,   true),
                    ("Final Decision",                        365,   false),
                },
                "MX" => new()
                {
                    ("Submission Filed",                        0,   false),
                    ("Admission",                              30,   false),
                    ("Technical Review Phase 1",              120,   false),
                    ("Additional Information Request (AIR)",  120,   true),
                    ("Technical Review Phase 2",              240,   false),
                    ("Resolution",                            365,   false),
                },
                "CO" => new()
                {
                    ("Submission Filed",                        0,   false),
                    ("Administrative Review",                  30,   false),
                    ("Technical Evaluation",                  120,   false),
                    ("Query (if applicable)",                 120,   true),
                    ("Decision",                              270,   false),
                },
                _ => new()
            };
        }
    }
}
