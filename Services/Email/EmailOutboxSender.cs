using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services.Email
{
    /// <summary>
    /// Sends the e-mail outbox in the background (every 30 seconds; "EmailOutbox:IntervalSeconds"). A failed message is
    /// retried after 1, 5, 15, 60 and 240 minutes, then marked Failed; the error is kept on the row and shown in System
    /// Settings. While SMTP is not configured nothing is sent and no attempt is used up.
    /// </summary>
    public class EmailOutboxSender : BackgroundService
    {
        public static readonly int[] RetryMinutes = { 1, 5, 15, 60, 240 };
        private const int Batch = 20;

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<EmailOutboxSender> _log;
        private readonly TimeSpan _interval;

        public EmailOutboxSender(IServiceScopeFactory scopes, ILogger<EmailOutboxSender> log, IConfiguration config)
        {
            _scopes = scopes;
            _log = log;
            _interval = TimeSpan.FromSeconds(Math.Max(1, config.GetValue("EmailOutbox:IntervalSeconds", 30)));
        }

        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await SendDueAsync(stop);
                }
                catch (Exception ex) when (!stop.IsCancellationRequested)
                {
                    _log.LogError(ex, "E-mail outbox run failed.");
                }
                try { await Task.Delay(_interval, stop); } catch (TaskCanceledException) { }
            }
        }

        public async Task SendDueAsync(CancellationToken stop)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(stop);
            if (!SmtpTransport.IsConfigured(settings))
                return;
            var transport = scope.ServiceProvider.GetRequiredService<SmtpTransport>();
            var now = DateTime.UtcNow;
            var due = await db.EmailOutbox
                .Where(m => m.Status == EmailOutboxStatus.Pending && m.NextAttemptOn <= now)
                .OrderBy(m => m.Id).Take(Batch).ToListAsync(stop);
            foreach (var m in due)
            {
                try
                {
                    await transport.SendAsync(settings!, m.ToAddress, m.Subject, m.HtmlBody, stop);
                    m.Status = EmailOutboxStatus.Sent;
                    m.SentOn = DateTime.UtcNow;
                    m.Attempts++;
                    m.LastError = null;
                }
                catch (Exception ex) when (!stop.IsCancellationRequested)
                {
                    m.Attempts++;
                    m.LastError = Short(ex);
                    if (m.Attempts > RetryMinutes.Length)
                        m.Status = EmailOutboxStatus.Failed;
                    else
                        m.NextAttemptOn = DateTime.UtcNow.AddMinutes(RetryMinutes[m.Attempts - 1]);
                    _log.LogWarning("E-mail {Id} ({Kind}) not sent, attempt {Attempts}: {Error}", m.Id, m.Kind, m.Attempts, m.LastError);
                }
                await db.SaveChangesAsync(stop);
            }
        }

        private static string Short(Exception ex)
        {
            var msg = ex switch
            {
                MailKit.Net.Smtp.SmtpCommandException c => $"{(int)c.StatusCode} {c.Message}",   // e.g. "451 Temporary failure"
                InvalidOperationException { InnerException: { } inner } => $"{ex.Message} ({inner.Message})",
                _ => ex.Message,
            };
            return msg.Length > 1000 ? msg[..1000] : msg;
        }
    }
}
