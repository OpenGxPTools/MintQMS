using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class PVPeriodicReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PVPeriodicReportsController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: PVPeriodicReports
        public async Task<IActionResult> Index()
        {
            var reports = await _context.PVPeriodicReport
                .Include(r => r.Product)
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            return View(reports);
        }

        // GET: PVPeriodicReports/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.PVPeriodicReport
                .Include(r => r.Product)
                .Include(r => r.LinkedAECases)
                    .ThenInclude(reg => reg.AdverseEvent)
                        .ThenInclude(ae => ae!.Patient)
                .Include(r => r.LinkedAECases)
                    .ThenInclude(reg => reg.AdverseEvent)
                        .ThenInclude(ae => ae!.SuspectProduct)
                .Include(r => r.AuditLogs)
                .Include(r => r.Attachments!.Where(a => !a.IsDeleted))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return NotFound();

            // AEs flagged for this product/period not yet linked
            var unlinked = await _context.AdverseEvent
                .Include(a => a.RegulatoryReport)
                .Where(a =>
                    a.RegulatoryReport != null &&
                    a.RegulatoryReport.IncludeInNextPSUR &&
                    a.RegulatoryReport.PSURId == null &&
                    (report.ProductId == null || a.SuspectProduct!.ProductId == report.ProductId))
                .ToListAsync();

            ViewBag.UnlinkedCases = unlinked;
            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.PSURAttachments.DeletedOnly().Where(f => f.PSURId == id).ToListAsync();
            return View(report);
        }

        // GET: PVPeriodicReports/Create
        public IActionResult Create()
        {
            ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && p.Status == Product.ProductStatus.Approved).OrderBy(p => p.Name), "Id", "Name");
            return View(new PVPeriodicReport
            {
                Status = PVPeriodicReport.PSURStatus.InPreparation,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = _userManager.GetUserName(User) ?? ""
            });
        }

        // POST: PVPeriodicReports/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Create(PVPeriodicReport report)
        {
            if (!ModelState.IsValid)
            {
                ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && p.Status == Product.ProductStatus.Approved).OrderBy(p => p.Name), "Id", "Name");
                return View(report);
            }

            report.CreatedBy = _userManager.GetUserName(User) ?? "";
            report.CreatedOn = DateTime.UtcNow;
            _context.PVPeriodicReport.Add(report);
            await _context.SaveChangesAsync();

            AddAuditLog(report.Id, "Created", null, report.Status.ToString());
            await _context.SaveChangesAsync();

            TempData["Success"] = "PSUR period created.";
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        // GET: PVPeriodicReports/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var report = await _context.PVPeriodicReport.FindAsync(id);
            if (report == null) return NotFound();

            ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && (p.Status == Product.ProductStatus.Approved || p.Id == report.ProductId)).OrderBy(p => p.Name), "Id", "Name", report.ProductId);
            return View(report);
        }

        // POST: PVPeriodicReports/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int id, PVPeriodicReport report)
        {
            if (id != report.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && (p.Status == Product.ProductStatus.Approved || p.Id == report.ProductId)).OrderBy(p => p.Name), "Id", "Name", report.ProductId);
                return View(report);
            }

            try
            {
                var existing = await _context.PVPeriodicReport.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
                if (existing == null) return NotFound();

                if (existing.Status == PVPeriodicReport.PSURStatus.Accepted)
                {
                    TempData["Error"] = "This report is Accepted and locked; it can no longer be edited.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Status is driven by the workflow actions (Submit/Accept are e-signature gated),
                // not the Edit form — preserve whatever the workflow has set.
                report.Status = existing.Status;

                // Field-level change tracking for the audit log
                var diff = new List<string>();
                AuditDiffHelper.Track(diff,     "Title",                   existing.Title,                 report.Title);
                AuditDiffHelper.Track(diff,     "Product (Id)",            existing.ProductId,             report.ProductId);
                AuditDiffHelper.TrackDate(diff, "Period Start",            existing.PeriodStart,           report.PeriodStart);
                AuditDiffHelper.TrackDate(diff, "Period End",              existing.PeriodEnd,             report.PeriodEnd);
                AuditDiffHelper.TrackDate(diff, "Due Date",                existing.DueDate,               report.DueDate);
                AuditDiffHelper.TrackDate(diff, "Submission Date",         existing.SubmissionDate,        report.SubmissionDate);
                AuditDiffHelper.Track(diff,     "TGA Reference Number",    existing.TGAReferenceNumber,    report.TGAReferenceNumber);
                AuditDiffHelper.Track(diff,     "Exposure Data",           existing.ExposureData,          report.ExposureData);
                AuditDiffHelper.Track(diff,     "Benefit/Risk Conclusion", existing.BenefitRiskConclusion, report.BenefitRiskConclusion);

                _context.Update(report);
                await _context.SaveChangesAsync();

                if (diff.Count > 0) AddAuditLog(id, "Edited", existing.Status.ToString(), report.Status.ToString(), AuditDiffHelper.Build(diff));
                await _context.SaveChangesAsync();
                TempData["Success"] = "PSUR updated.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.PVPeriodicReport.AnyAsync(r => r.Id == id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: PVPeriodicReports/LinkCase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> LinkCase(int psurId, int aeRegId)
        {
            var reg = await _context.AERegulatoryReport.FindAsync(aeRegId);
            if (reg != null)
            {
                reg.PSURId = psurId;
                await _context.SaveChangesAsync();
                AddAuditLog(psurId, "Case Linked", null, null, $"AE Regulatory Report #{aeRegId} linked");
                await _context.SaveChangesAsync();
            }
            TempData["Success"] = "Case linked to PSUR.";
            return RedirectToAction(nameof(Details), new { id = psurId });
        }

        // GET: PVPeriodicReports/ExportPSURData/5
        [HttpGet]
        public async Task<IActionResult> ExportPSURData(int id)
        {
            var report = await _context.PVPeriodicReport
                .Include(r => r.Product)
                .Include(r => r.LinkedAECases)
                    .ThenInclude(reg => reg.AdverseEvent)
                        .ThenInclude(ae => ae!.Patient)
                .Include(r => r.LinkedAECases)
                    .ThenInclude(reg => reg.AdverseEvent)
                        .ThenInclude(ae => ae!.SuspectProduct)
                .Include(r => r.LinkedAECases)
                    .ThenInclude(reg => reg.AdverseEvent)
                        .ThenInclude(ae => ae!.Reporter)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return NotFound();

            // Literature searches for the period
            var litSearches = (report.PeriodStart.HasValue && report.PeriodEnd.HasValue)
                ? await _context.PVLiteratureSearch
                    .Where(s => s.SearchDate >= report.PeriodStart.Value &&
                                s.SearchDate <= report.PeriodEnd.Value)
                    .OrderBy(s => s.SearchDate)
                    .ToListAsync()
                : new List<PVLiteratureSearch>();

            using var wb = new XLWorkbook();

            // ── Sheet 1: PSUR Summary ──────────────────────────────────────
            var ws1 = wb.Worksheets.Add("PSUR Summary");
            ws1.Cell(1, 1).Value = "PSUR — Periodic Safety Update Report";
            ws1.Cell(1, 1).Style.Font.Bold = true;
            ws1.Cell(1, 1).Style.Font.FontSize = 14;

            var summaryRows = new[]
            {
                ("PSUR Title",            report.Title),
                ("Product",               report.Product?.Name ?? "All Products"),
                ("Period Start",          report.PeriodStart?.ToString("dd MMM yyyy") ?? ""),
                ("Period End",            report.PeriodEnd?.ToString("dd MMM yyyy") ?? ""),
                ("Due Date",              report.DueDate?.ToString("dd MMM yyyy") ?? ""),
                ("Submission Date",       report.SubmissionDate?.ToString("dd MMM yyyy") ?? ""),
                ("TGA Reference",         report.TGAReferenceNumber ?? ""),
                ("Status",                report.Status.ToString()),
                ("Total Linked AE Cases", (report.LinkedAECases?.Count ?? 0).ToString()),
                ("Serious Cases",         (report.LinkedAECases?.Count(r => r.AdverseEvent?.IsSerious == true) ?? 0).ToString()),
                ("Exposure Data",         report.ExposureData ?? ""),
                ("Benefit-Risk Conclusion", report.BenefitRiskConclusion ?? ""),
                ("Export Date",           SiteTime.Now.ToString("dd MMM yyyy HH:mm") + " " + SiteTime.Label)
            };

            for (int i = 0; i < summaryRows.Length; i++)
            {
                ws1.Cell(i + 3, 1).Value = summaryRows[i].Item1;
                ws1.Cell(i + 3, 1).Style.Font.Bold = true;
                ws1.Cell(i + 3, 2).Value = summaryRows[i].Item2;
            }
            ws1.Column(1).Width = 28;
            ws1.Column(2).Width = 55;

            // ── Sheet 2: ICH E2C(R2) Case Tabulation ──────────────────────
            var ws2 = wb.Worksheets.Add("Case Tabulation");
            var headers = new[]
            {
                "Case #", "AE Number", "Country", "Report Date", "Report Type",
                "Patient Age", "Patient Sex", "Suspect Drug", "Indication",
                "Dose / Route", "Treatment Start", "Treatment End",
                "Reaction (MedDRA PT)", "MedDRA SOC", "AE Start", "AE End",
                "Serious", "Seriousness Criteria", "Outcome", "Causality",
                "Expectedness", "Regulatory Status", "TGA Submission Date"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws2.Cell(1, i + 1).Value = headers[i];
                ws2.Cell(1, i + 1).Style.Font.Bold = true;
                ws2.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#003F7D");
                ws2.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var regRep in report.LinkedAECases ?? Enumerable.Empty<AERegulatoryReport>())
            {
                var ae = regRep.AdverseEvent;
                if (ae == null) continue;
                var pat = ae.Patient;
                var sp = ae.SuspectProduct;
                var rep = ae.Reporter;

                ws2.Cell(row, 1).Value  = row - 1;
                ws2.Cell(row, 2).Value  = ae.AENumber;
                ws2.Cell(row, 3).Value  = rep?.Country ?? "";
                ws2.Cell(row, 4).Value  = ae.ReportDate.ToSite().ToString("dd MMM yyyy");
                ws2.Cell(row, 5).Value  = ae.ReportType.ToString();
                ws2.Cell(row, 6).Value  = pat?.Age.HasValue == true
                    ? $"{pat.Age} {pat.AgeUnit}" : "";
                ws2.Cell(row, 7).Value  = pat?.Sex?.ToString() ?? "";
                ws2.Cell(row, 8).Value  = sp?.ProductName ?? "";
                ws2.Cell(row, 9).Value  = sp?.Indication ?? "";
                ws2.Cell(row, 10).Value = string.Join(" ", new[]
                    { sp?.Dose, sp?.DoseUnit, sp?.RouteOfAdministration }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                ws2.Cell(row, 11).Value = sp?.TreatmentStartDate?.ToString("dd MMM yyyy") ?? "";
                ws2.Cell(row, 12).Value = sp?.TreatmentEndDate?.ToString("dd MMM yyyy") ?? "";
                ws2.Cell(row, 13).Value = ae.MedDRAPT ?? "";
                ws2.Cell(row, 14).Value = ae.MedDRASOC ?? "";
                ws2.Cell(row, 15).Value = ae.AEStartDate?.ToString("dd MMM yyyy") ?? "";
                ws2.Cell(row, 16).Value = ae.AEEndDate?.ToString("dd MMM yyyy") ?? "";
                ws2.Cell(row, 17).Value = ae.IsSerious ? "Yes" : "No";
                ws2.Cell(row, 18).Value = ae.SeriousnessCriteria.ToString();
                ws2.Cell(row, 19).Value = ae.Outcome?.ToString() ?? "";
                ws2.Cell(row, 20).Value = ae.Causality?.ToString() ?? "";
                ws2.Cell(row, 21).Value = ae.Expectedness?.ToString() ?? "";
                ws2.Cell(row, 22).Value = regRep.Status.ToString();
                ws2.Cell(row, 23).Value = regRep.TGASubmissionDate?.ToString("dd MMM yyyy") ?? "";

                if (ae.IsSerious)
                    ws2.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3F3");

                row++;
            }

            ws2.Row(1).Height = 16;
            ws2.Columns().AdjustToContents(8, 50);

            // ── Sheet 3: Literature Search Log ────────────────────────────
            var ws3 = wb.Worksheets.Add("Literature Searches");
            var litHeaders = new[]
            {
                "Search Date", "Searched By", "Databases", "Search Terms",
                "Date Range From", "Date Range To", "Total Results",
                "Relevant Results", "AEs Identified", "Notes"
            };

            for (int i = 0; i < litHeaders.Length; i++)
            {
                ws3.Cell(1, i + 1).Value = litHeaders[i];
                ws3.Cell(1, i + 1).Style.Font.Bold = true;
                ws3.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#003F7D");
                ws3.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int litRow = 2;
            foreach (var ls in litSearches)
            {
                ws3.Cell(litRow, 1).Value  = ls.SearchDate.ToString("dd MMM yyyy");
                ws3.Cell(litRow, 2).Value  = ls.SearchedBy;
                ws3.Cell(litRow, 3).Value  = ls.DatabasesSearched ?? "";
                ws3.Cell(litRow, 4).Value  = ls.SearchTerms ?? "";
                ws3.Cell(litRow, 5).Value  = ls.DateRangeFrom?.ToString("dd MMM yyyy") ?? "";
                ws3.Cell(litRow, 6).Value  = ls.DateRangeTo?.ToString("dd MMM yyyy") ?? "";
                ws3.Cell(litRow, 7).Value  = ls.ResultsCount;
                ws3.Cell(litRow, 8).Value  = ls.RelevantResults;
                ws3.Cell(litRow, 9).Value  = ls.AEsIdentified;
                ws3.Cell(litRow, 10).Value = ls.Notes ?? "";
                litRow++;
            }

            ws3.Columns().AdjustToContents(8, 50);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var bytes = ms.ToArray();
            var fileName = $"PSUR_{report.Title.Replace(" ", "_")}_{SiteTime.Now:yyyyMMdd}.xlsx";

            try
            {
                AddAuditLog(id, "Exported", null, null, $"Excel export: {fileName}");
                await _context.SaveChangesAsync();
            }
            catch { /* audit log failure must not block the file download */ }

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ── Attachments ───────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UploadAttachment(int psurId, IFormFile file, string? description)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Details), new { id = psurId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed.";
                return RedirectToAction(nameof(Details), new { id = psurId });
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File size exceeds the 25 MB limit.";
                return RedirectToAction(nameof(Details), new { id = psurId });
            }

            var report = await _context.PVPeriodicReport.FindAsync(psurId);
            if (report == null) return NotFound();

            if (report.Status == PVPeriodicReport.PSURStatus.Accepted)
            {
                TempData["Error"] = "This report has been accepted by the authority and is finalised — attachments cannot be added.";
                return RedirectToAction(nameof(Details), new { id = psurId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "psur", psurId.ToString());
            Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            _context.PSURAttachments.Add(new PSURAttachment
            {
                PSURId      = psurId,
                FileName    = file.FileName,
                FilePath    = $"/uploads/psur/{psurId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize    = file.Length,
                FileHash    = fileHash,
                UploadedBy  = User.Identity?.Name ?? "Unknown",
                UploadedAt  = DateTime.UtcNow,
                Description = description
            });
            AddAuditLog(psurId, "AttachmentUploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded.";
            return RedirectToAction(nameof(Details), new { id = psurId });
        }

        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();
            var attachment = await _context.PSURAttachments.ForDownload(deleted).FirstOrDefaultAsync(a => a.Id == id);
            if (attachment == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToAction(nameof(Details), new { id = attachment.PSURId });
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                AddAuditLog(attachment.PSURId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                return RedirectToAction(nameof(Details), new { id = attachment.PSURId });
            }

            AddAuditLog(attachment.PSURId, (deleted ? "DeletedAttachmentDownloaded" : "AttachmentDownloaded"), null, null, attachment.FileName);
            await _context.SaveChangesAsync();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.RegulatoryAttachment, null, "PSUR",
                attachment.PSURId.ToString(), null,
                User.Identity?.Name ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(fileBytes, Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteAttachment(int id, int psurId, string? deletionReason)
        {
            var attachment = await _context.PSURAttachments.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                psurId = attachment.PSURId;

                var report = await _context.PVPeriodicReport.FindAsync(psurId);
                if (report != null && report.Status == PVPeriodicReport.PSURStatus.Accepted)
                {
                    TempData["Error"] = "This report has been accepted by the authority and is finalised — attachments cannot be removed.";
                    return RedirectToAction(nameof(Details), new { id = psurId });
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    return RedirectToAction(nameof(Details), new { id = psurId });
                }

                attachment.IsDeleted      = true;
                attachment.DeletedBy      = User.Identity?.Name ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                AddAuditLog(psurId, "AttachmentDeleted", null, null, $"{attachment.FileName} — Reason: {attachment.DeletionReason}");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attachment deleted.";
            }
            return RedirectToAction(nameof(Details), new { id = psurId });
        }

        // ── Workflow transitions ─────────────────────────────────────────────

        // POST: one-click steps that do not require an e-signature (InPreparation ⇄ UnderReview)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Transition(int id, PVPeriodicReport.PSURStatus newStatus)
        {
            var report = await _context.PVPeriodicReport.FindAsync(id);
            if (report == null) return NotFound();

            bool allowed =
                (report.Status == PVPeriodicReport.PSURStatus.InPreparation && newStatus == PVPeriodicReport.PSURStatus.UnderReview) ||
                (report.Status == PVPeriodicReport.PSURStatus.UnderReview   && newStatus == PVPeriodicReport.PSURStatus.InPreparation);
            if (!allowed)
            {
                TempData["Error"] = "Invalid status transition.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await ChangeStatus(report, newStatus);
            TempData["Success"] = $"Status updated to {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Submit to authority (password-gated e-signature)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,RegulatoryUser")]
        public Task<IActionResult> SubmitReport(int id, string email, string pwd)
            => GatedTransition(id, email, pwd,
                PVPeriodicReport.PSURStatus.UnderReview, PVPeriodicReport.PSURStatus.Submitted);

        // POST: Mark accepted by authority (password-gated e-signature; locks the report)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,RegulatoryUser")]
        public Task<IActionResult> AcceptReport(int id, string email, string pwd)
            => GatedTransition(id, email, pwd,
                PVPeriodicReport.PSURStatus.Submitted, PVPeriodicReport.PSURStatus.Accepted);

        private async Task<IActionResult> GatedTransition(
            int id, string email, string pwd,
            PVPeriodicReport.PSURStatus from, PVPeriodicReport.PSURStatus to)
        {
            // E-signature must be made with the signed-in user's own credentials
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user = null;
            if (user == null || !await _userManager.CheckPasswordAsync(user, pwd))
            {
                TempData["Error"] = "Invalid credentials. Action not authorised.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var report = await _context.PVPeriodicReport.FindAsync(id);
            if (report == null) return NotFound();

            if (report.Status != from)
            {
                TempData["Error"] = $"Can only move to {to} from {from}.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Stamp the submission date when the report is submitted.
            if (to == PVPeriodicReport.PSURStatus.Submitted)
                report.SubmissionDate ??= SiteTime.Today;

            await ChangeStatus(report, to);
            TempData["Success"] = $"Status updated to {to}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task ChangeStatus(PVPeriodicReport report, PVPeriodicReport.PSURStatus newStatus)
        {
            var oldStatus = report.Status.ToString();
            report.Status = newStatus;
            AddAuditLog(report.Id, "Status Changed", oldStatus, newStatus.ToString());
            await _context.SaveChangesAsync();
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.PVPsurAuditLog.Add(new PVPsurAuditLog
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
