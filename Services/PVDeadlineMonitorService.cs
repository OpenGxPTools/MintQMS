using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    /// <summary>
    /// Daily background job that checks PV regulatory deadlines and writes
    /// audit log alert entries for overdue and approaching-deadline cases.
    /// </summary>
    public class PVDeadlineMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PVDeadlineMonitorService> _logger;

        // Run once on startup, then every 24 hours
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

        public PVDeadlineMonitorService(
            IServiceScopeFactory scopeFactory,
            ILogger<PVDeadlineMonitorService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PV Deadline Monitor started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunChecksAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during PV deadline monitor check.");
                }

                try { await Task.Delay(CheckInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunSignalCheckAsync(ApplicationDbContext context, CancellationToken ct)
        {
            var cutoff = SiteTime.Today.AddDays(-90);

            // Group AEs by MedDRA PT in the last 90 days — flag if ≥3 cases
            var signalGroups = await context.AdverseEvent
                .Where(a => a.MedDRAPT != null &&
                            a.ReportDate >= cutoff &&
                            a.Status != AdverseEvent.AEStatus.Voided)
                .GroupBy(a => a.MedDRAPT)
                .Where(g => g.Count() >= 3)
                .Select(g => new { PT = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var signal in signalGroups)
            {
                // Auto-flag SignalDetected on affected AEs not yet flagged
                var affectedAEs = await context.AdverseEvent
                    .Where(a => a.MedDRAPT == signal.PT &&
                                a.ReportDate >= cutoff &&
                                !a.SignalDetected &&
                                a.Status != AdverseEvent.AEStatus.Voided)
                    .ToListAsync(ct);

                foreach (var ae in affectedAEs)
                    ae.SignalDetected = true;

                // Create PVSignalReview if no open review exists for this PT
                var openReviewExists = await context.PVSignalReview
                    .AnyAsync(r => r.MedDRAPT == signal.PT &&
                                   r.Status != PVSignalReview.SignalStatus.Closed &&
                                   r.Status != PVSignalReview.SignalStatus.Refuted, ct);

                if (!openReviewExists)
                {
                    var review = new PVSignalReview
                    {
                        MedDRAPT     = signal.PT!,
                        DetectedOn   = DateTime.UtcNow,
                        DetectedBy   = "System",
                        TriggerCount = signal.Count,
                        Status       = PVSignalReview.SignalStatus.Open,
                        CreatedOn    = DateTime.UtcNow
                    };
                    context.PVSignalReview.Add(review);
                    // Flush to get the generated Id before adding the audit log
                    await context.SaveChangesAsync(ct);

                    context.PVSignalAuditLog.Add(new PVSignalAuditLog
                    {
                        ParentId    = review.Id,
                        Action      = "Created",
                        FromStatus  = null,
                        ToStatus    = PVSignalReview.SignalStatus.Open.ToString(),
                        PerformedBy = "System",
                        PerformedOn = DateTime.UtcNow,
                        Notes       = $"{signal.Count} cases detected in 90 days"
                    });
                    _logger.LogWarning("PV Signal detected: {PT} — {Count} cases in 90 days.", signal.PT, signal.Count);
                }
            }

            if (signalGroups.Any())
                await context.SaveChangesAsync(ct);
        }

        private async Task RunChecksAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var regulatory = scope.ServiceProvider.GetRequiredService<PVRegulatoryService>();

            var today = SiteTime.Today;

            // ── Overdue cases ────────────────────────────────────────────────
            var overdue = await regulatory.GetOverdueCases().ToListAsync(ct);
            foreach (var ae in overdue)
            {
                // Write alert only once per day per case
                var alreadyLogged = await context.AEAuditLog.AnyAsync(
                    l => l.AdverseEventId == ae.Id &&
                         l.Action == "DeadlineOverdue" &&
                         l.PerformedOn.Date == today, ct);

                if (!alreadyLogged)
                {
                    context.AEAuditLog.Add(new AEAuditLog
                    {
                        AdverseEventId = ae.Id,
                        Action = "DeadlineOverdue",
                        NewValue = $"TGA deadline {ae.TGADeadlineDate!.Value:dd MMM yyyy} passed with no submission recorded.",
                        PerformedBy = "System",
                        PerformedOn = DateTime.UtcNow
                    });
                    _logger.LogWarning("PV Monitor: Overdue case {AENumber} (deadline {Deadline:dd MMM yyyy})",
                        ae.AENumber, ae.TGADeadlineDate!.Value);
                }
            }

            // ── Approaching deadline (≤5 days) ───────────────────────────────
            var approaching = await regulatory.GetApproachingDeadlineCases(5).ToListAsync(ct);
            foreach (var ae in approaching)
            {
                var daysLeft = (ae.TGADeadlineDate!.Value.Date - today).Days;
                var action = daysLeft <= 2 ? "DeadlineCritical" : "DeadlineWarning";

                var alreadyLogged = await context.AEAuditLog.AnyAsync(
                    l => l.AdverseEventId == ae.Id &&
                         l.Action == action &&
                         l.PerformedOn.Date == today, ct);

                if (!alreadyLogged)
                {
                    context.AEAuditLog.Add(new AEAuditLog
                    {
                        AdverseEventId = ae.Id,
                        Action = action,
                        NewValue = $"TGA deadline in {daysLeft} day(s) ({ae.TGADeadlineDate!.Value:dd MMM yyyy}).",
                        PerformedBy = "System",
                        PerformedOn = DateTime.UtcNow
                    });
                    _logger.LogWarning("PV Monitor: {Action} — {AENumber} has {Days} day(s) remaining.",
                        action, ae.AENumber, daysLeft);
                }
            }

            if (overdue.Any() || approaching.Any())
                await context.SaveChangesAsync(ct);

            _logger.LogInformation("PV deadline check complete: {Overdue} overdue, {Approaching} approaching.",
                overdue.Count, approaching.Count);

            // ── Signal detection ─────────────────────────────────────────
            await RunSignalCheckAsync(context, ct);
        }
    }
}
