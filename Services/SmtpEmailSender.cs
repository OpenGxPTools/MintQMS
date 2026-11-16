using Microsoft.AspNetCore.Identity.UI.Services;
using OpenQMS.Data;
using OpenQMS.Services.Email;

namespace OpenQMS.Services
{
    /// <summary>Account e-mails (address confirmation, password reset): sent at once, as before.</summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IServiceScopeFactory scopeFactory, ILogger<SmtpEmailSender> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await context.SystemSettings.FindAsync(1);

            if (!SmtpTransport.IsConfigured(settings))
            {
                _logger.LogWarning("Email not sent to {Email}: SMTP is not configured in System Settings.", email);
                return;
            }

            try
            {
                await scope.ServiceProvider.GetRequiredService<SmtpTransport>().SendAsync(settings!, email, subject, htmlMessage);
                _logger.LogInformation("Email sent to {Email} — subject: {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} via {Host}:{Port}. Check SMTP settings in System Settings.",
                    email, settings!.SmtpHost, settings.SmtpPort);
            }
        }
    }
}
