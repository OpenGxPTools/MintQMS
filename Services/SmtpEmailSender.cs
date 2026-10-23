using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using OpenQMS.Data;

namespace OpenQMS.Services
{
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

            if (settings == null
                || string.IsNullOrWhiteSpace(settings.SmtpHost)
                || string.IsNullOrWhiteSpace(settings.EmailFromAddress))
            {
                _logger.LogWarning("Email not sent to {Email}: SMTP is not configured in System Settings.", email);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                string.IsNullOrWhiteSpace(settings.EmailFromName) ? settings.EmailFromAddress : settings.EmailFromName,
                settings.EmailFromAddress));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlMessage };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls);

                if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
                    await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword ?? string.Empty);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent to {Email} — subject: {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} via {Host}:{Port}. Check SMTP settings in System Settings.",
                    email, settings.SmtpHost, settings.SmtpPort);
            }
        }
    }
}
