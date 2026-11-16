using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using OpenQMS.Models;

namespace OpenQMS.Services.Email
{
    /// <summary>Sends one message through the SMTP server in System Settings (STARTTLS). Throws on any failure.</summary>
    public class SmtpTransport
    {
        private readonly SmtpSecret _secret;

        public SmtpTransport(SmtpSecret secret) => _secret = secret;

        public static bool IsConfigured(SystemSettings? s) =>
            s != null && !string.IsNullOrWhiteSpace(s.SmtpHost) && !string.IsNullOrWhiteSpace(s.EmailFromAddress);

        public async Task SendAsync(SystemSettings settings, string to, string subject, string html, CancellationToken ct = default)
        {
            if (!IsConfigured(settings))
                throw new InvalidOperationException("SMTP is not configured in System Settings.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                string.IsNullOrWhiteSpace(settings.EmailFromName) ? settings.EmailFromAddress : settings.EmailFromName,
                settings.EmailFromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = html };

            using var client = new SmtpClient { Timeout = 30000 };
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls, ct);
            if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
                await client.AuthenticateAsync(settings.SmtpUsername, _secret.Reveal(settings.SmtpPassword) ?? string.Empty, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}
