using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models.LC;

namespace OpenQMS.Services
{
    /// <summary>
    /// Background service that runs daily at 08:00 AEST to:
    ///   1. Auto-transition IssuedActive records to RenewalDue when within RenewalLeadTimeDays of ExpiryDate.
    ///   2. Auto-transition active records to Expired when ExpiryDate has passed.
    ///   3. Alert on overdue RFI responses.
    ///   4. Alert on upcoming ISO surveillance audits (within 60 days).
    ///   5. Alert on LCCondition due dates (60 / 30 / 14 day windows).
    /// </summary>
    public class RenewalLeadTimeMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RenewalLeadTimeMonitorService> _logger;

        public RenewalLeadTimeMonitorService(
            IServiceProvider serviceProvider,
            ILogger<RenewalLeadTimeMonitorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger          = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RenewalLeadTimeMonitorService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Schedule: run daily at 08:00 AEST

                var now     = SiteTime.Now;   // site time zone (System Settings)
                var nextRun = now.Date.AddHours(8);
                if (now >= nextRun) nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("RenewalLeadTimeMonitorService: next run at {NextRun} AEST (in {Delay:hh\\:mm}).",
                    nextRun, delay);

                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    using var scope  = _serviceProvider.CreateScope();
                    var context      = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var notifier     = scope.ServiceProvider.GetRequiredService<LicCertNotificationService>();

                    _logger.LogInformation("RenewalLeadTimeMonitorService: running daily checks at {Time} UTC.", DateTime.UtcNow);

                    await CheckRenewalLeadTimes(context, notifier);
                    await CheckExpired(context, notifier);
                    await CheckRFIOverdue(context, notifier);
                    await CheckSurveillanceAuditsDue(context, notifier);
                    await CheckConditionsDue(context, notifier);
                }
                catch (Exception ex)
                {
                    // An unhandled exception in a BackgroundService stops the whole host;
                    // log and retry at the next scheduled run instead.
                    _logger.LogError(ex, "Error during licence renewal monitor check.");
                }
            }
        }

        // ── 1. Renewal Lead Time Alerts (notify-only) ────────────────────────
        // Mirrors the RegIntel module: the monitor never changes status, it only
        // raises notifications. Users start a renewal manually via "Initiate
        // Renewal Now". Covers active, dated records approaching (but not past)
        // their expiry; past-expiry records are handled by CheckExpired below.

        private async Task CheckRenewalLeadTimes(
            ApplicationDbContext context,
            LicCertNotificationService notifier)
        {
            var today = SiteTime.Today;

            var activeStatuses = new[] { LCStatus.IssuedActive, LCStatus.ConditionsApplied, LCStatus.RenewalDue };

            var records = await context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted
                         && activeStatuses.Contains(r.Status)
                         && r.ExpiryDate.HasValue)
                .ToListAsync();

            int alerted = 0;

            foreach (var r in records)
            {
                int daysToExpiry = (r.ExpiryDate!.Value.Date - today).Days;
                int lead         = r.RenewalLeadTimeDays > 0 ? r.RenewalLeadTimeDays : 90;

                if (daysToExpiry >= 0 && daysToExpiry <= lead)
                {
                    string level   = daysToExpiry <= 30 ? "CRITICAL" : "WARNING";
                    string message = $"Expiry in {daysToExpiry} day(s) ({r.ExpiryDate.Value:dd MMM yyyy}). Renewal application must be progressed.";

                    await notifier.SendAsync("RenewalAlert", level, r.LCNumber, message,
                        new[] { r.InternalResponsiblePersonId ?? "LicenceManager" });

                    alerted++;
                }
            }

            _logger.LogInformation("CheckRenewalLeadTimes: {Alerted} renewal alert(s) sent.", alerted);
        }

        // ── 2. Overdue Expiry Alerts (notify-only) ───────────────────────────
        // Flags records whose ExpiryDate has passed. Status is NOT changed and the
        // record is NOT locked automatically — a user must decide to renew,
        // withdraw, or mark expired (consistent with the RegIntel module).

        private async Task CheckExpired(
            ApplicationDbContext context,
            LicCertNotificationService notifier)
        {
            var today = SiteTime.Today;

            var expirableStatuses = new[] { LCStatus.IssuedActive, LCStatus.ConditionsApplied, LCStatus.RenewalDue };

            var records = await context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted
                         && expirableStatuses.Contains(r.Status)
                         && r.ExpiryDate.HasValue
                         && r.ExpiryDate.Value.Date < today)
                .ToListAsync();

            foreach (var r in records)
            {
                int overdueDays = (today - r.ExpiryDate!.Value.Date).Days;
                string message  = $"Record EXPIRED {overdueDays} day(s) ago ({r.ExpiryDate.Value:dd MMM yyyy}). " +
                                  "Initiate renewal, withdrawal, or mark the record expired. " +
                                  "Linked product applications may be at risk.";

                await notifier.SendAsync("CriticalExpiry", "EXPIRED", r.LCNumber, message,
                    new[] { r.InternalResponsiblePersonId ?? "LicenceDirector" });
            }

            _logger.LogInformation("CheckExpired: {Count} overdue record(s) flagged.", records.Count);
        }

        // ── 3. RFI Overdue Alerts ─────────────────────────────────────────────

        private async Task CheckRFIOverdue(
            ApplicationDbContext context,
            LicCertNotificationService notifier)
        {
            var today = SiteTime.Today;

            var records = await context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted
                         && r.RFIResponseDueDate.HasValue
                         && r.RFIResponseDueDate.Value.Date < today
                         && r.RFIResponseSubmittedDate == null
                         && r.Status != LCStatus.Expired
                         && r.Status != LCStatus.Suspended
                         && r.Status != LCStatus.Withdrawn)
                .ToListAsync();

            foreach (var r in records)
            {
                int overdueDays = (today - r.RFIResponseDueDate!.Value.Date).Days;
                string level    = overdueDays > 30 ? "CRITICAL" : "WARNING";
                string message  = $"RFI response overdue by {overdueDays} day(s). " +
                                  $"Response was due {r.RFIResponseDueDate.Value:dd MMM yyyy}. " +
                                  "TGA assessment clock may be paused. Submit response or contact authority immediately.";

                await notifier.SendAsync("RFIOverdue", level, r.LCNumber, message,
                    new[] { r.InternalResponsiblePersonId ?? "LicenceManager" });
            }

            _logger.LogInformation("CheckRFIOverdue: {Count} record(s) with overdue RFI response.", records.Count);
        }

        // ── 4. ISO Surveillance Audit Due Alerts ──────────────────────────────

        private async Task CheckSurveillanceAuditsDue(
            ApplicationDbContext context,
            LicCertNotificationService notifier)
        {
            var today    = SiteTime.Today;
            var warnDate = today.AddDays(60);

            var records = await context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted
                         && r.Category == LCCategory.ISOPrivateCertification
                         && r.NextSurveillanceAuditDate.HasValue
                         && r.NextSurveillanceAuditDate.Value.Date <= warnDate
                         && r.Status != LCStatus.Expired
                         && r.Status != LCStatus.Withdrawn)
                .ToListAsync();

            foreach (var r in records)
            {
                int daysToAudit = (r.NextSurveillanceAuditDate!.Value.Date - today).Days;
                string level    = daysToAudit < 0 ? "OVERDUE" : daysToAudit <= 14 ? "CRITICAL" : "WARNING";
                string message  = daysToAudit < 0
                    ? $"ISO surveillance audit OVERDUE by {Math.Abs(daysToAudit)} day(s). " +
                      $"Audit was scheduled {r.NextSurveillanceAuditDate.Value:dd MMM yyyy}. " +
                      "Contact certification body immediately to reschedule."
                    : $"ISO surveillance audit due in {daysToAudit} day(s) ({r.NextSurveillanceAuditDate.Value:dd MMM yyyy}). " +
                      "Ensure QMS documentation is up to date and audit programme is confirmed.";

                await notifier.SendAsync("SurveillanceAuditDue", level, r.LCNumber, message,
                    new[] { r.InternalResponsiblePersonId ?? "LicenceManager" });
            }

            _logger.LogInformation("CheckSurveillanceAuditsDue: {Count} ISO cert(s) with upcoming or overdue audit.", records.Count);
        }

        // ── 5. LCCondition Due Date Alerts ────────────────────────────────────

        private async Task CheckConditionsDue(
            ApplicationDbContext context,
            LicCertNotificationService notifier)
        {
            var today = SiteTime.Today;

            // Alert windows: 60 days, 30 days, 14 days
            var alertDays = new[] { 60, 30, 14 };
            var outerWindow = today.AddDays(60);

            var conditions = await context.LCConditions
                .Where(c => !c.IsDeleted
                         && c.Status == LCConditionStatus.Active
                         && c.DueDate.HasValue
                         && c.DueDate.Value.Date <= outerWindow)
                .Include(c => c.Record)
                .ToListAsync();

            int alerted = 0;

            foreach (var cond in conditions)
            {
                int daysToCondition = (cond.DueDate!.Value.Date - today).Days;

                // Only alert on specific day-windows or if overdue
                bool shouldAlert = daysToCondition < 0
                    || alertDays.Any(w => daysToCondition == w);

                if (!shouldAlert) continue;

                string level   = daysToCondition < 0 ? "OVERDUE" : daysToCondition <= 14 ? "CRITICAL" : "WARNING";
                string message = daysToCondition < 0
                    ? $"Condition OVERDUE by {Math.Abs(daysToCondition)} day(s): \"{cond.ConditionDescription[..Math.Min(80, cond.ConditionDescription.Length)]}...\". Immediate action required."
                    : $"Condition due in {daysToCondition} day(s) ({cond.DueDate.Value:dd MMM yyyy}): \"{cond.ConditionDescription[..Math.Min(80, cond.ConditionDescription.Length)]}...\".";

                await notifier.SendAsync("ConditionDue", level,
                    cond.Record?.LCNumber ?? $"Record #{cond.LicenceCertificationRecordId}",
                    message,
                    new[] { cond.Record?.InternalResponsiblePersonId ?? "LicenceManager" });

                alerted++;
            }

            _logger.LogInformation("CheckConditionsDue: {Count} condition alert(s) sent.", alerted);
        }
    }
}
