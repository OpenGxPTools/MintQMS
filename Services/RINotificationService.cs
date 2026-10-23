using OpenQMS.Data;

namespace OpenQMS.Services
{
    /// <summary>
    /// Sends structured RegIntel deadline and compliance alerts.
    /// Currently logs to ILogger; wire up email/Teams/in-app in a future stage.
    /// </summary>
    public class RINotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RINotificationService> _logger;

        public RINotificationService(ApplicationDbContext context, ILogger<RINotificationService> logger)
        {
            _context = context;
            _logger  = logger;
        }

        /// <summary>
        /// Sends a structured alert for a specific alert type and severity level.
        /// </summary>
        /// <param name="alertType">Category: TargetDecisionDate | ExpiryRenewal | ConditionOfApproval | AccessPathwayExpiry | SixMonthlyReport | ClockStopOverdue</param>
        /// <param name="level">Severity: WARNING | CRITICAL | OVERDUE | EXPIRED</param>
        /// <param name="message">Human-readable alert message.</param>
        /// <param name="recipients">Recipient identifiers (names or email addresses).</param>
        public Task SendAsync(string alertType, string level, string message, IEnumerable<string> recipients)
        {
            var recipientList = string.Join(", ", recipients);

            switch (level)
            {
                case "OVERDUE":
                case "EXPIRED":
                case "CRITICAL":
                    _logger.LogError("[RegIntel {AlertType}] [{Level}] {Message} → Recipients: {Recipients}",
                        alertType, level, message, recipientList);
                    break;

                default:
                    _logger.LogWarning("[RegIntel {AlertType}] [{Level}] {Message} → Recipients: {Recipients}",
                        alertType, level, message, recipientList);
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>Legacy overload — routes to SendAsync with level = INFO.</summary>
        public Task SendAsync(string alertType, string message, IEnumerable<string> recipients)
            => SendAsync(alertType, "INFO", message, recipients);
    }
}
