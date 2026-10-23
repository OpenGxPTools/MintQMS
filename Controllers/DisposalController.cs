using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    // Gap 13: Cross-module data retention and disposal management.
    [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager")]
    public class DisposalController : Controller
    {
        private readonly ApplicationDbContext  _context;
        private readonly RetentionService      _retention;
        private readonly UserManager<AppUser>  _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public DisposalController(
            ApplicationDbContext context,
            RetentionService retention,
            UserManager<AppUser> userManager,
            IPasswordHasher<AppUser> passwordHasher)
        {
            _context        = context;
            _retention      = retention;
            _userManager    = userManager;
            _passwordHasher = passwordHasher;
        }

        // GET: /Disposal
        public async Task<IActionResult> Index()
        {
            var items    = await _retention.GetEligibleItemsAsync();
            var settings = await _retention.GetSettingsAsync();
            ViewBag.CooldownDays = settings.RetentionHoldCooldownDays;
            return View(items);
        }

        // GET: /Disposal/AuditLog
        public async Task<IActionResult> AuditLog()
        {
            var log = await _context.DisposalAuditLog
                .OrderByDescending(l => l.ActionOn)
                .ToListAsync();
            return View(log);
        }

        // POST: /Disposal/SetHold
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetHold(
            string module, int recordId,
            RetentionHoldReason reason, string? comments)
        {
            var userName = User.Identity?.Name ?? string.Empty;
            var reference = await ApplyHoldAsync(module, recordId, reason, comments, userName);
            if (reference == null)
                return NotFound();

            _context.DisposalAuditLog.Add(new DisposalAuditLog
            {
                Module          = module,
                RecordId        = recordId,
                RecordReference = reference,
                Action          = DisposalAction.HoldSet.ToString(),
                ActionBy        = userName,
                ActionOn        = DateTime.UtcNow,
                Notes           = $"Reason: {reason}. {comments}"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Retention hold set on {module} record {reference}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Disposal/RemoveHold  — Administrator only, requires password re-entry
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveHold(
            string module, int recordId,
            string InputEmail, string InputPassword, string removalReason)
        {
            // Electronic signature verification (21 CFR Part 11 §11.100(a) + §11.200)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, InputEmail, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Index));
            }
            if (_passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, InputPassword)
                    == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Electronic signature verification failed.";
                return RedirectToAction(nameof(Index));
            }

            var settings  = await _retention.GetSettingsAsync();
            var userName  = User.Identity?.Name ?? string.Empty;
            var reference = await ClearHoldAsync(module, recordId, userName, removalReason, settings.RetentionHoldCooldownDays);
            if (reference == null)
                return NotFound();

            _context.DisposalAuditLog.Add(new DisposalAuditLog
            {
                Module          = module,
                RecordId        = recordId,
                RecordReference = reference,
                Action          = DisposalAction.HoldRemoved.ToString(),
                ActionBy        = userName,
                ActionOn        = DateTime.UtcNow,
                Notes           = $"Removal reason: {removalReason}. Cooldown until: {SiteTime.Now.AddDays(settings.RetentionHoldCooldownDays):yyyy-MM-dd}. E-sig verified by: {InputEmail}"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Retention hold removed. Record will be available for disposal after {settings.RetentionHoldCooldownDays}-day cooldown.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Disposal/Dispose  — Administrator only
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispose(string[] selections, string InputEmail, string InputPassword)
        {
            if (selections == null || selections.Length == 0)
            {
                TempData["Error"] = "No records selected.";
                return RedirectToAction(nameof(Index));
            }

            // Electronic signature verification (21 CFR Part 11 §11.100(a) + §11.200)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, InputEmail, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Index));
            }
            if (string.IsNullOrEmpty(InputPassword) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, InputPassword)
                    == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Electronic signature verification failed.";
                return RedirectToAction(nameof(Index));
            }

            var userName = User.Identity?.Name ?? string.Empty;
            var now      = DateTime.UtcNow;
            int disposed = 0;
            int blocked  = 0;

            foreach (var sel in selections)
            {
                var parts = sel.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int recId))
                    continue;

                var module    = parts[0];
                var reference = await SoftDeleteAsync(module, recId, userName, now);

                if (reference == null)
                {
                    // Record had retention hold or cooldown — blocked
                    blocked++;
                    _context.DisposalAuditLog.Add(new DisposalAuditLog
                    {
                        Module          = module,
                        RecordId        = recId,
                        RecordReference = $"{module}:{recId}",
                        Action          = DisposalAction.DisposalAttemptBlocked.ToString(),
                        ActionBy        = userName,
                        ActionOn        = now,
                        Notes           = "Blocked: active retention hold or cooldown period."
                    });
                }
                else
                {
                    disposed++;
                    _context.DisposalAuditLog.Add(new DisposalAuditLog
                    {
                        Module          = module,
                        RecordId        = recId,
                        RecordReference = reference,
                        Action          = DisposalAction.Disposed.ToString(),
                        ActionBy        = userName,
                        ActionOn        = now,
                        Notes           = $"Record soft-deleted as part of approved disposal. E-sig verified by: {InputEmail}."
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{disposed} record(s) disposed.";
            if (blocked > 0)
                TempData["Warning"] = $"{blocked} record(s) were skipped — active retention hold or cooldown.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Disposal/ExportReport
        public async Task<IActionResult> ExportReport()
        {
            var items    = await _retention.GetEligibleItemsAsync();
            var userName = User.Identity?.Name ?? string.Empty;
            var now      = DateTime.UtcNow;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Disposal Eligible Records");

            // Header
            var headers = new[]
            {
                "Module", "Reference", "Title", "Terminal Status",
                "Terminal Date", "Retain Until", "Retention Hold",
                "Hold Reason", "Hold Comments", "Hold Set By", "Hold Set On",
                "Cooldown Until", "Eligible for Disposal"
            };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
            }

            int row = 2;
            foreach (var item in items)
            {
                ws.Cell(row, 1).Value  = item.Module;
                ws.Cell(row, 2).Value  = item.RecordReference;
                ws.Cell(row, 3).Value  = item.Title;
                ws.Cell(row, 4).Value  = item.TerminalStatus;
                ws.Cell(row, 5).Value  = item.TerminalDate.ToSite().ToString("yyyy-MM-dd");
                ws.Cell(row, 6).Value  = item.RetainUntil.ToString("yyyy-MM-dd");
                ws.Cell(row, 7).Value  = item.RetentionExtended ? "YES" : "No";
                ws.Cell(row, 8).Value  = item.RetentionExtendedReason?.ToString() ?? "";
                ws.Cell(row, 9).Value  = item.RetentionExtendedComments ?? "";
                ws.Cell(row, 10).Value = item.RetentionExtendedBy ?? "";
                ws.Cell(row, 11).Value = item.RetentionExtendedOn?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(row, 12).Value = item.RetentionHoldCooldownUntil?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(row, 13).Value = item.IsEligibleForDisposal ? "Yes" : "No";

                // Highlight held records
                if (item.RetentionExtended)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightYellow;
                else if (item.CooldownActive)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightBlue;

                row++;
            }

            ws.Columns().AdjustToContents();

            // Audit the export
            _context.DisposalAuditLog.Add(new DisposalAuditLog
            {
                Module          = "ALL",
                RecordId        = 0,
                RecordReference = "EXPORT",
                Action          = DisposalAction.ExportGenerated.ToString(),
                ActionBy        = userName,
                ActionOn        = now,
                Notes           = $"Exported {items.Count} disposal-eligible records."
            });
            await _context.SaveChangesAsync();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var fileName = $"DisposalEligible_{now:yyyyMMdd_HHmm}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        // Returns human-readable reference on success, null if record not found.
        private async Task<string?> ApplyHoldAsync(
            string module, int id,
            RetentionHoldReason reason, string? comments, string byUser)
        {
            var now = DateTime.UtcNow;
            switch (module)
            {
                case "CAPA":
                    var capa = await _context.Capa.FindAsync(id);
                    if (capa == null) return null;
                    capa.RetentionExtended = true; capa.RetentionExtendedReason = reason;
                    capa.RetentionExtendedComments = comments; capa.RetentionExtendedBy = byUser;
                    capa.RetentionExtendedOn = now;
                    return capa.CapaId;
                case "Deviation":
                    var dev = await _context.Deviation.FindAsync(id);
                    if (dev == null) return null;
                    dev.RetentionExtended = true; dev.RetentionExtendedReason = reason;
                    dev.RetentionExtendedComments = comments; dev.RetentionExtendedBy = byUser;
                    dev.RetentionExtendedOn = now;
                    return dev.DeviationId;
                case "Change":
                    var chg = await _context.Change.FindAsync(id);
                    if (chg == null) return null;
                    chg.RetentionExtended = true; chg.RetentionExtendedReason = reason;
                    chg.RetentionExtendedComments = comments; chg.RetentionExtendedBy = byUser;
                    chg.RetentionExtendedOn = now;
                    return chg.ChangeId;
                case "Complaint":
                    var cmp = await _context.Complaint.FindAsync(id);
                    if (cmp == null) return null;
                    cmp.RetentionExtended = true; cmp.RetentionExtendedReason = reason;
                    cmp.RetentionExtendedComments = comments; cmp.RetentionExtendedBy = byUser;
                    cmp.RetentionExtendedOn = now;
                    return cmp.ComplaintId;
                case "Risk":
                    var risk = await _context.Risk.FindAsync(id);
                    if (risk == null) return null;
                    risk.RetentionExtended = true; risk.RetentionExtendedReason = reason;
                    risk.RetentionExtendedComments = comments; risk.RetentionExtendedBy = byUser;
                    risk.RetentionExtendedOn = now;
                    return risk.RiskId;
                case "Audit":
                    var audit = await _context.Audit.FindAsync(id);
                    if (audit == null) return null;
                    audit.RetentionExtended = true; audit.RetentionExtendedReason = reason;
                    audit.RetentionExtendedComments = comments; audit.RetentionExtendedBy = byUser;
                    audit.RetentionExtendedOn = now;
                    return audit.Id.ToString();
                case "Maintenance":
                    var maint = await _context.Maintenance.FindAsync(id);
                    if (maint == null) return null;
                    maint.RetentionExtended = true; maint.RetentionExtendedReason = reason;
                    maint.RetentionExtendedComments = comments; maint.RetentionExtendedBy = byUser;
                    maint.RetentionExtendedOn = now;
                    return maint.Id.ToString();
                case "Training":
                    var train = await _context.Training.FindAsync(id);
                    if (train == null) return null;
                    train.RetentionExtended = true; train.RetentionExtendedReason = reason;
                    train.RetentionExtendedComments = comments; train.RetentionExtendedBy = byUser;
                    train.RetentionExtendedOn = now;
                    return train.TrainingId;
                case "Document":
                    var doc = await _context.AppDocument.FindAsync(id);
                    if (doc == null) return null;
                    doc.RetentionExtended = true; doc.RetentionExtendedReason = reason;
                    doc.RetentionExtendedComments = comments; doc.RetentionExtendedBy = byUser;
                    doc.RetentionExtendedOn = now;
                    return doc.DocumentId;
                case "RI":
                    var ri = await _context.RegulatoryApplications.FindAsync(id);
                    if (ri == null) return null;
                    ri.RetentionExtended = true; ri.RetentionExtendedReason = reason;
                    ri.RetentionExtendedComments = comments; ri.RetentionExtendedBy = byUser;
                    ri.RetentionExtendedOn = now;
                    return ri.RINumber;
                case "LC":
                    var lc = await _context.LicenceCertificationRecords.FindAsync(id);
                    if (lc == null) return null;
                    lc.RetentionExtended = true; lc.RetentionExtendedReason = reason;
                    lc.RetentionExtendedComments = comments; lc.RetentionExtendedBy = byUser;
                    lc.RetentionExtendedOn = now;
                    return lc.LCNumber;
                default:
                    return null;
            }
        }

        // Clears the hold, sets cooldown. Returns reference or null.
        private async Task<string?> ClearHoldAsync(
            string module, int id,
            string byUser, string removalReason, int cooldownDays)
        {
            var now      = DateTime.UtcNow;
            var cooldown = now.AddDays(cooldownDays);

            switch (module)
            {
                case "CAPA":
                    var capa = await _context.Capa.FindAsync(id);
                    if (capa == null) return null;
                    capa.RetentionExtended = false;
                    capa.RetentionExtendedRemovalReason = removalReason;
                    capa.RetentionExtendedRemovedBy = byUser;
                    capa.RetentionExtendedRemovedOn = now;
                    capa.RetentionHoldCooldownUntil = cooldown;
                    return capa.CapaId;
                case "Deviation":
                    var dev = await _context.Deviation.FindAsync(id);
                    if (dev == null) return null;
                    dev.RetentionExtended = false;
                    dev.RetentionExtendedRemovalReason = removalReason;
                    dev.RetentionExtendedRemovedBy = byUser;
                    dev.RetentionExtendedRemovedOn = now;
                    dev.RetentionHoldCooldownUntil = cooldown;
                    return dev.DeviationId;
                case "Change":
                    var chg = await _context.Change.FindAsync(id);
                    if (chg == null) return null;
                    chg.RetentionExtended = false;
                    chg.RetentionExtendedRemovalReason = removalReason;
                    chg.RetentionExtendedRemovedBy = byUser;
                    chg.RetentionExtendedRemovedOn = now;
                    chg.RetentionHoldCooldownUntil = cooldown;
                    return chg.ChangeId;
                case "Complaint":
                    var cmp = await _context.Complaint.FindAsync(id);
                    if (cmp == null) return null;
                    cmp.RetentionExtended = false;
                    cmp.RetentionExtendedRemovalReason = removalReason;
                    cmp.RetentionExtendedRemovedBy = byUser;
                    cmp.RetentionExtendedRemovedOn = now;
                    cmp.RetentionHoldCooldownUntil = cooldown;
                    return cmp.ComplaintId;
                case "Risk":
                    var risk = await _context.Risk.FindAsync(id);
                    if (risk == null) return null;
                    risk.RetentionExtended = false;
                    risk.RetentionExtendedRemovalReason = removalReason;
                    risk.RetentionExtendedRemovedBy = byUser;
                    risk.RetentionExtendedRemovedOn = now;
                    risk.RetentionHoldCooldownUntil = cooldown;
                    return risk.RiskId;
                case "Audit":
                    var audit = await _context.Audit.FindAsync(id);
                    if (audit == null) return null;
                    audit.RetentionExtended = false;
                    audit.RetentionExtendedRemovalReason = removalReason;
                    audit.RetentionExtendedRemovedBy = byUser;
                    audit.RetentionExtendedRemovedOn = now;
                    audit.RetentionHoldCooldownUntil = cooldown;
                    return audit.Id.ToString();
                case "Maintenance":
                    var maint = await _context.Maintenance.FindAsync(id);
                    if (maint == null) return null;
                    maint.RetentionExtended = false;
                    maint.RetentionExtendedRemovalReason = removalReason;
                    maint.RetentionExtendedRemovedBy = byUser;
                    maint.RetentionExtendedRemovedOn = now;
                    maint.RetentionHoldCooldownUntil = cooldown;
                    return maint.Id.ToString();
                case "Training":
                    var train = await _context.Training.FindAsync(id);
                    if (train == null) return null;
                    train.RetentionExtended = false;
                    train.RetentionExtendedRemovalReason = removalReason;
                    train.RetentionExtendedRemovedBy = byUser;
                    train.RetentionExtendedRemovedOn = now;
                    train.RetentionHoldCooldownUntil = cooldown;
                    return train.TrainingId;
                case "Document":
                    var doc = await _context.AppDocument.FindAsync(id);
                    if (doc == null) return null;
                    doc.RetentionExtended = false;
                    doc.RetentionExtendedRemovalReason = removalReason;
                    doc.RetentionExtendedRemovedBy = byUser;
                    doc.RetentionExtendedRemovedOn = now;
                    doc.RetentionHoldCooldownUntil = cooldown;
                    return doc.DocumentId;
                case "RI":
                    var ri = await _context.RegulatoryApplications.FindAsync(id);
                    if (ri == null) return null;
                    ri.RetentionExtended = false;
                    ri.RetentionExtendedRemovalReason = removalReason;
                    ri.RetentionExtendedRemovedBy = byUser;
                    ri.RetentionExtendedRemovedOn = now;
                    ri.RetentionHoldCooldownUntil = cooldown;
                    return ri.RINumber;
                case "LC":
                    var lc = await _context.LicenceCertificationRecords.FindAsync(id);
                    if (lc == null) return null;
                    lc.RetentionExtended = false;
                    lc.RetentionExtendedRemovalReason = removalReason;
                    lc.RetentionExtendedRemovedBy = byUser;
                    lc.RetentionExtendedRemovedOn = now;
                    lc.RetentionHoldCooldownUntil = cooldown;
                    return lc.LCNumber;
                default:
                    return null;
            }
        }

        // Soft-deletes a record. Returns reference on success, null if blocked (hold/cooldown).
        private async Task<string?> SoftDeleteAsync(string module, int id, string byUser, DateTime now)
        {
            switch (module)
            {
                case "CAPA":
                    var capa = await _context.Capa.FindAsync(id);
                    if (capa == null || capa.RetentionExtended || (capa.RetentionHoldCooldownUntil.HasValue && capa.RetentionHoldCooldownUntil > now)) return null;
                    capa.IsDeleted = true; capa.DeletedBy = byUser; capa.DeletedOn = now;
                    return capa.CapaId;
                case "Deviation":
                    var dev = await _context.Deviation.FindAsync(id);
                    if (dev == null || dev.RetentionExtended || (dev.RetentionHoldCooldownUntil.HasValue && dev.RetentionHoldCooldownUntil > now)) return null;
                    dev.IsDeleted = true; dev.DeletedBy = byUser; dev.DeletedOn = now;
                    return dev.DeviationId;
                case "Change":
                    var chg = await _context.Change.FindAsync(id);
                    if (chg == null || chg.RetentionExtended || (chg.RetentionHoldCooldownUntil.HasValue && chg.RetentionHoldCooldownUntil > now)) return null;
                    chg.IsDeleted = true; chg.DeletedBy = byUser; chg.DeletedOn = now;
                    return chg.ChangeId;
                case "Complaint":
                    var cmp = await _context.Complaint.FindAsync(id);
                    if (cmp == null || cmp.RetentionExtended || (cmp.RetentionHoldCooldownUntil.HasValue && cmp.RetentionHoldCooldownUntil > now)) return null;
                    cmp.IsDeleted = true; cmp.DeletedBy = byUser; cmp.DeletedOn = now;
                    return cmp.ComplaintId;
                case "Risk":
                    var risk = await _context.Risk.FindAsync(id);
                    if (risk == null || risk.RetentionExtended || (risk.RetentionHoldCooldownUntil.HasValue && risk.RetentionHoldCooldownUntil > now)) return null;
                    risk.IsDeleted = true; risk.DeletedBy = byUser; risk.DeletedOn = now;
                    return risk.RiskId;
                case "Audit":
                    var audit = await _context.Audit.FindAsync(id);
                    if (audit == null || audit.RetentionExtended || (audit.RetentionHoldCooldownUntil.HasValue && audit.RetentionHoldCooldownUntil > now)) return null;
                    audit.IsDeleted = true; audit.DeletedBy = byUser; audit.DeletedOn = now;
                    return audit.Id.ToString();
                case "Maintenance":
                    var maint = await _context.Maintenance.FindAsync(id);
                    if (maint == null || maint.RetentionExtended || (maint.RetentionHoldCooldownUntil.HasValue && maint.RetentionHoldCooldownUntil > now)) return null;
                    maint.IsDeleted = true; maint.DeletedBy = byUser; maint.DeletedOn = now;
                    return maint.Id.ToString();
                case "Training":
                    var train = await _context.Training.FindAsync(id);
                    if (train == null || train.RetentionExtended || (train.RetentionHoldCooldownUntil.HasValue && train.RetentionHoldCooldownUntil > now)) return null;
                    train.IsDeleted = true; train.DeletedBy = byUser; train.DeletedOn = now;
                    return train.TrainingId;
                case "Document":
                    var doc = await _context.AppDocument.FindAsync(id);
                    if (doc == null || doc.RetentionExtended || (doc.RetentionHoldCooldownUntil.HasValue && doc.RetentionHoldCooldownUntil > now)) return null;
                    doc.IsDeleted = true; doc.DeletedBy = byUser; doc.DeletedOn = now;
                    return doc.DocumentId;
                case "RI":
                    var ri = await _context.RegulatoryApplications.FindAsync(id);
                    if (ri == null || ri.RetentionExtended || (ri.RetentionHoldCooldownUntil.HasValue && ri.RetentionHoldCooldownUntil > now)) return null;
                    ri.IsDeleted = true; ri.DeletedBy = byUser; ri.DeletedOn = now;
                    return ri.RINumber;
                case "LC":
                    var lc = await _context.LicenceCertificationRecords.FindAsync(id);
                    if (lc == null || lc.RetentionExtended || (lc.RetentionHoldCooldownUntil.HasValue && lc.RetentionHoldCooldownUntil > now)) return null;
                    lc.IsDeleted = true; lc.DeletedBy = byUser; lc.DeletedOn = now;
                    return lc.LCNumber;
                default:
                    return null;
            }
        }
    }
}
