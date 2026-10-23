namespace OpenQMS.Services
{
    /// <summary>
    /// Sends structured LicCert renewal, compliance, and expiry alerts.
    /// Currently logs to ILogger; wire up email / Teams / in-app notifications in a future iteration.
    /// </summary>
    public class LicCertNotificationService
    {
        private readonly ILogger<LicCertNotificationService> _logger;

        public LicCertNotificationService(ILogger<LicCertNotificationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Sends a structured alert.
        /// </summary>
        /// <param name="alertType">
        ///   RenewalAlert | ExpiryEscalation | CriticalExpiry | Suspension |
        ///   RFIOverdue | SurveillanceAuditDue | ConditionDue
        /// </param>
        /// <param name="level">WARNING | CRITICAL | OVERDUE | EXPIRED</param>
        /// <param name="lcNumber">The LC record reference number.</param>
        /// <param name="message">Human-readable alert message.</param>
        /// <param name="recipients">Recipient identifiers (names or emails).</param>
        public Task SendAsync(
            string alertType,
            string level,
            string lcNumber,
            string message,
            IEnumerable<string> recipients)
        {
            var recipientList = string.Join(", ", recipients);

            switch (level)
            {
                case "EXPIRED":
                case "OVERDUE":
                case "CRITICAL":
                    _logger.LogError(
                        "[LicCert {AlertType}] [{Level}] {LCNumber}: {Message} → {Recipients}",
                        alertType, level, lcNumber, message, recipientList);
                    break;

                default:
                    _logger.LogWarning(
                        "[LicCert {AlertType}] [{Level}] {LCNumber}: {Message} → {Recipients}",
                        alertType, level, lcNumber, message, recipientList);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
