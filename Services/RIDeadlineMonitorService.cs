using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models.RI;

namespace OpenQMS.Services
{
    /// <summary>
    /// Background service that runs daily at 08:00 AEST to check RegIntel deadlines,
    /// renewal alerts, condition due dates, access pathway expiries, and supply reports.
    /// </summary>
    public class RIDeadlineMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RIDeadlineMonitorService> _logger;

        public RIDeadlineMonitorService(IServiceProvider serviceProvider, ILogger<RIDeadlineMonitorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger          = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RIDeadlineMonitorService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Run once per day at 08:00 site time

                var now     = SiteTime.Now;   // site time zone (System Settings)
                var nextRun = now.Date.AddHours(8);
                if (now >= nextRun) nextRun = nextRun.AddDays(1);

                try { await Task.Delay(nextRun - now, stoppingToken); }
                catch (OperationCanceledException) { break; }
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    using var scope             = _serviceProvider.CreateScope();
                    var context                 = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var notificationService     = scope.ServiceProvider.GetRequiredService<RINotificationService>();

                    _logger.LogInformation("RIDeadlineMonitorService: running daily checks at {Time}", DateTime.UtcNow);

                    await CheckTargetDecisionDates(context, notificationService);
                    await CheckExpiryAndRenewals(context, notificationService);
                    await CheckConditionsOfApproval(context, notificationService);
                    await CheckAccessPathwayExpiries(context, notificationService);
                    await CheckSixMonthlyReports(context, notificationService);
                    await CheckClockStopOverdueResponses(context, notificationService);
                }
                catch (Exception ex)
                {
                    // An unhandled exception in a BackgroundService stops the whole host;
                    // log and retry at the next scheduled run instead.
                    _logger.LogError(ex, "Error during RI deadline monitor check.");
                }
            }
        }

        // ── Check Methods ─────────────────────────────────────────────────────

        private async Task CheckTargetDecisionDates(
            ApplicationDbContext context,
            RINotificationService notificationService)
        {
            var today    = SiteTime.Today;
            var warnDate = today.AddDays(30);

            var apps = await context.RegulatoryApplications
                .Where(a => !a.IsDeleted &&
                            a.Status == RIApplicationStatus.UnderReview &&
                            a.TargetDecisionDate.HasValue &&
                            a.TargetDecisionDate <= warnDate)
                .Include(a => a.Jurisdiction)
                .ToListAsync();

            foreach (var app in apps)
            {
                int days = (app.TargetDecisionDate!.Value.Date - today).Days;
                string level = days < 0 ? "OVERDUE" : days <= 7 ? "CRITICAL" : "WARNING";
                string msg = days < 0
                    ? $"{app.RINumber} ({app.Jurisdiction?.Authority}): Target decision date OVERDUE by {Math.Abs(days)} day(s). Escalate immediately."
                    : $"{app.RINumber} ({app.Jurisdiction?.Authority}): Target decision date in {days} day(s) ({app.TargetDecisionDate.Value:dd MMM yyyy}).";

                await notificationService.SendAsync("TargetDecisionDate", level, msg,
                    new[] { app.InternalRALead ?? "RA Team" });
            }

            _logger.LogInformation("CheckTargetDecisionDates: {Count} application(s) flagged.", apps.Count);
        }

        private async Task CheckExpiryAndRenewals(
            ApplicationDbContext context,
            RINotificationService notificationService)
        {
            var today    = SiteTime.Today;
            var warnDate = today.AddDays(90);

            var apps = await context.RegulatoryApplications
                .Where(a => !a.IsDeleted &&
                            (a.Status == RIApplicationStatus.Approved ||
                             a.Status == RIApplicationStatus.ConditionsActive) &&
                            a.ExpiryDate.HasValue &&
                            a.ExpiryDate <= warnDate)
                .Include(a => a.Jurisdiction)
                .ToListAsync();

            foreach (var app in apps)
            {
                int days = (app.ExpiryDate!.Value.Date - today).Days;
                string level = days < 0 ? "EXPIRED" : days <= 30 ? "CRITICAL" : "WARNING";
                string msg = days < 0
                    ? $"{app.RINumber} ({app.Jurisdiction?.Authority}): Authorisation EXPIRED {Math.Abs(days)} day(s) ago. Initiate emergency renewal."
                    : $"{app.RINumber} ({app.Jurisdiction?.Authority}): Authorisation expires in {days} day(s) ({app.ExpiryDate.Value:dd MMM yyyy}). Initiate renewal.";

                await notificationService.SendAsync("ExpiryRenewal", level, msg,
                    new[] { app.InternalRALead ?? "RA Team" });
            }

            _logger.LogInformation("CheckExpiryAndRenewals: {Count} application(s) flagged.", apps.Count);
        }

        private async Task CheckConditionsOfApproval(
            ApplicationDbContext context,
            RINotificationService notificationService)
        {
            var today    = SiteTime.Today;
            var warnDate = today.AddDays(30);

            var conditions = await context.RIConditionsOfApproval
                .Where(c => (c.Status == RIConditionStatus.Outstanding ||
                             c.Status == RIConditionStatus.InProgress) &&
                             c.DueDate.HasValue &&
                             c.DueDate <= warnDate)
                .Include(c => c.Application)
                .ThenInclude(a => a.Jurisdiction)
                .ToListAsync();

            foreach (var cond in conditions)
            {
                int days = (cond.DueDate!.Value.Date - today).Days;
                string level = days < 0 ? "OVERDUE" : days <= 7 ? "CRITICAL" : "WARNING";
                string msg = days < 0
                    ? $"{cond.Application?.RINumber}: Condition '{cond.ConditionType}' OVERDUE by {Math.Abs(days)} day(s). Immediate action required."
                    : $"{cond.Application?.RINumber}: Condition '{cond.ConditionType}' due in {days} day(s) ({cond.DueDate.Value:dd MMM yyyy}).";

                await notificationService.SendAsync("ConditionOfApproval", level, msg,
                    new[] { cond.Application?.InternalRALead ?? "RA Team" });
            }

            _logger.LogInformation("CheckConditionsOfApproval: {Count} condition(s) flagged.", conditions.Count);
        }

        private async Task CheckAccessPathwayExpiries(
            ApplicationDbContext context,
            RINotificationService notificationService)
        {
            var today    = SiteTime.Today;
            var warnDate = today.AddDays(60);

            var pathways = await context.RIAccessPathwayRecords
                .Where(ap => (ap.Status == RIAccessPathwayStatus.Active ||
                              ap.Status == RIAccessPathwayStatus.Approved) &&
                              ap.ExpiryDate.HasValue &&
                              ap.ExpiryDate <= warnDate)
                .Include(ap => ap.Application)
                .ThenInclude(a => a.Jurisdiction)
                .ToListAsync();

            foreach (var ap in pathways)
            {
                int days = (ap.ExpiryDate!.Value.Date - today).Days;
                string level = days < 0 ? "EXPIRED" : days <= 14 ? "CRITICAL" : "WARNING";
                string msg = days < 0
                    ? $"{ap.AccessNumber} ({ap.PathwayType}): Access pathway EXPIRED {Math.Abs(days)} day(s) ago. Supply must cease."
                    : $"{ap.AccessNumber} ({ap.PathwayType}): Access pathway expires in {days} day(s) ({ap.ExpiryDate.Value:dd MMM yyyy}). Initiate renewal.";

                await notificationService.SendAsync("AccessPathwayExpiry", level, msg,
                    new[] { ap.Application?.InternalRALead ?? "RA Team" });
            }

            _logger.LogInformation("CheckAccessPathwayExpiries: {Count} pathway(s) flagged.", pathways.Count);
        }

        private async Task CheckSixMonthlyReports(
            ApplicationDbContext context,
            RINotificationService notificationService)
        {
            var today    = SiteTime.Today;
            var warnDate = today.AddDays(30);

            var pathways = await context.RIAccessPathwayRecords
                .Where(ap => ap.AEReportingRequired &&
                             ap.NextSixMonthlyReportDue.HasValue &&
                             ap.NextSixMonthlyReportDue <= warnDate &&
                             (ap.Status == RIAccessPathwayStatus.Active ||
                              ap.Status == RIAccessPathwayStatus.Approved))
                .Include(ap => ap.Application)
                .ToListAsync();

            foreach (var ap in pathways)
            {
                int days = (ap.NextSixMonthlyReportDue!.Value.Date - today).Days;
                string level = days < 0 ? "OVERDUE" : days <= 7 ? "CRITICAL" : "WARNING";
                string msg = days < 0
                    ? $"{ap.AccessNumber}: Six-monthly supply report OVERDUE by {Math.Abs(days)} day(s). Submit immediately."
                    : $"{ap.AccessNumber}: Six-monthly supply report due in {days} day(s) ({ap.NextSixMonthlyReportDue.Value:dd MMM yyyy}).";

                await notificationService.SendAsync("SixMonthlyReport", level, msg,
                    new[] { ap.Application?.InternalRALead ?? "RA Team" });
            }

            _logger.LogInformation("CheckSixMonthlyReports: {Count} pathway(s) flagged.", pathways.Count);
        }

        private async Task CheckClockStopOverdueResponses(
            ApplicationDbContext context,
            RINotificationService notificationService)
        {
            // Flag applications in ClockStop status for more than 90 days
            var warnDate = DateTime.UtcNow.AddDays(-90);

            var apps = await context.RegulatoryApplications
                .Where(a => !a.IsDeleted &&
                            a.Status == RIApplicationStatus.ClockStop &&
                            a.ModifiedAt.HasValue &&
                            a.ModifiedAt <= warnDate)
                .Include(a => a.Jurisdiction)
                .ToListAsync();

            foreach (var app in apps)
            {
                int days = (int)(DateTime.UtcNow - app.ModifiedAt!.Value).TotalDays;
                string msg = $"{app.RINumber} ({app.Jurisdiction?.Authority}): In Clock-Stop status for {days} day(s). " +
                             "Verify applicant response has been submitted and restart clock.";

                await notificationService.SendAsync("ClockStopOverdue", "WARNING", msg,
                    new[] { app.InternalRALead ?? "RA Team" });
            }

            _logger.LogInformation("CheckClockStopOverdueResponses: {Count} application(s) flagged.", apps.Count);
        }
    }
}
