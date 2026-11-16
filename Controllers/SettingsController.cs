using OpenQMS.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using System.Text.Json;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,AuthorisedPerson,Auditor")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly string _backupStatusFile;
        private readonly OpenQMS.Localization.Languages _languages;
        private readonly OpenQMS.Services.Email.SmtpSecret _smtpSecret;
        private readonly OpenQMS.Services.Email.SmtpTransport _smtp;

        private static readonly string[] AllowedLogoExtensions = { ".png", ".jpg", ".jpeg", ".gif" };
        private const long MaxLogoBytes = 2 * 1024 * 1024; // 2 MB

        public SettingsController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration,
            OpenQMS.Localization.Languages languages, OpenQMS.Services.Email.SmtpSecret smtpSecret,
            OpenQMS.Services.Email.SmtpTransport smtp)
        {
            _smtpSecret = smtpSecret;
            _smtp = smtp;
            _context = context;
            _languages = languages;
            _env = env;
            _backupStatusFile = configuration["BackupStatusFile"] ?? "/var/backups/mintqms/backup_status.json";
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            var settings = await GetOrCreateSettingsAsync();
            ViewBag.CanEdit = User.IsInRole(Constants.AdministratorsRole);
            ViewBag.BackupStatus = ReadBackupStatus();
            await SetOutboxStatusAsync();
            return View(settings);
        }

        // POST: Settings/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Save(SystemSettings model, IFormFile? logoFile)
        {
            // Load existing settings first so LogoPath is available on any error return
            var settings = await GetOrCreateSettingsAsync();
            var changedBy = User.Identity?.Name ?? "System";
            var now = DateTime.UtcNow;

            // Re-validate only the fields we care about
            ModelState.Remove(nameof(SystemSettings.LogoPath));
            ModelState.Remove(nameof(SystemSettings.SmtpHost));
            ModelState.Remove(nameof(SystemSettings.SmtpUsername));
            ModelState.Remove(nameof(SystemSettings.SmtpPassword));
            ModelState.Remove(nameof(SystemSettings.EmailFromAddress));
            ModelState.Remove(nameof(SystemSettings.EmailFromName));
            ModelState.Remove(nameof(SystemSettings.SiteWebAddress));

            // Site web address: an absolute http(s) address, stored without a trailing slash (used for e-mail links).
            model.SiteWebAddress = string.IsNullOrWhiteSpace(model.SiteWebAddress) ? null : model.SiteWebAddress.Trim().TrimEnd('/');
            if (model.SiteWebAddress != null
                && (!Uri.TryCreate(model.SiteWebAddress, UriKind.Absolute, out var site) || (site.Scheme != "https" && site.Scheme != "http")
                    || model.SiteWebAddress.Length > 300))
                ModelState.AddModelError(nameof(SystemSettings.SiteWebAddress), this.T("Enter the full web address, e.g. https://qms.example.com"));
            // Assignment e-mails need a mail server and a sender address.
            if (model.SendAssignmentEmails && (string.IsNullOrWhiteSpace(model.SmtpHost) || string.IsNullOrWhiteSpace(model.EmailFromAddress)))
                ModelState.AddModelError(nameof(SystemSettings.SendAssignmentEmails), this.T("Set the SMTP host and the From address before turning on assignment e-mails."));

            if (!SiteTime.IsValid(model.TimeZoneId))
                ModelState.AddModelError(nameof(SystemSettings.TimeZoneId), this.T("Unknown time zone."));

            // Only an installed language (appsettings "Localization:Languages"), stored in canonical form.
            var siteLanguage = _languages.Find(model.SiteLanguage);
            if (siteLanguage == null)
                ModelState.AddModelError(nameof(SystemSettings.SiteLanguage), this.T("This language is not installed on this server."));
            else
                model.SiteLanguage = siteLanguage;

            if (!ModelState.IsValid)
            {
                ViewBag.CanEdit = true;
                model.LogoPath = settings.LogoPath; // preserve current logo on re-render
                model.SmtpPassword = settings.SmtpPassword;   // only "set / not set" is shown
                await SetOutboxStatusAsync();
                return View("Index", model);
            }

            // Handle logo upload
            if (logoFile != null && logoFile.Length > 0)
            {
                var ext = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
                if (!AllowedLogoExtensions.Contains(ext))
                {
                    ModelState.AddModelError("logoFile", this.T("Only PNG, JPG, or GIF files are allowed."));
                    ViewBag.CanEdit = true;
                    model.LogoPath = settings.LogoPath;
                    return View("Index", model);
                }
                if (logoFile.Length > MaxLogoBytes)
                {
                    ModelState.AddModelError("logoFile", this.T("Logo must be 2 MB or smaller."));
                    ViewBag.CanEdit = true;
                    model.LogoPath = settings.LogoPath;
                    return View("Index", model);
                }

                var webRoot = _env.WebRootPath
                    ?? throw new InvalidOperationException("WebRootPath is not configured.");
                var uploadsDir = Path.Combine(webRoot, "uploads", "logo");
                Directory.CreateDirectory(uploadsDir);
                var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await logoFile.CopyToAsync(stream);

                var newLogoPath = $"/uploads/logo/{fileName}";
                LogChange(settings.LogoPath, newLogoPath, "LogoPath", changedBy, now);
                settings.LogoPath = newLogoPath;
            }

            // Compare and log each text/numeric field
            LogChange(settings.OrganisationName, model.OrganisationName, "OrganisationName", changedBy, now);
            LogChange(settings.OrganisationAddress, model.OrganisationAddress, "OrganisationAddress", changedBy, now);
            LogChange(settings.TimeZoneId, model.TimeZoneId, "TimeZoneId", changedBy, now);
            LogChange(settings.SiteLanguage, model.SiteLanguage, "SiteLanguage", changedBy, now);
            LogChange(settings.AllowUserLanguageChoice.ToString(), model.AllowUserLanguageChoice.ToString(), "AllowUserLanguageChoice", changedBy, now);
            LogChange(settings.DefaultPageSize.ToString(), model.DefaultPageSize.ToString(), "DefaultPageSize", changedBy, now);
            LogChange(settings.LcExpiryWarningDays.ToString(), model.LcExpiryWarningDays.ToString(), "LcExpiryWarningDays", changedBy, now);
            LogChange(settings.MaintenanceDueWarningDays.ToString(), model.MaintenanceDueWarningDays.ToString(), "MaintenanceDueWarningDays", changedBy, now);
            LogChange(settings.TrainingDueWarningDays.ToString(), model.TrainingDueWarningDays.ToString(), "TrainingDueWarningDays", changedBy, now);
            LogChange(settings.DefaultCapaResolutionDays.ToString(), model.DefaultCapaResolutionDays.ToString(), "DefaultCapaResolutionDays", changedBy, now);
            LogChange(settings.DefaultDeviationResolutionDays.ToString(), model.DefaultDeviationResolutionDays.ToString(), "DefaultDeviationResolutionDays", changedBy, now);
            LogChange(settings.DefaultComplaintResolutionDays.ToString(), model.DefaultComplaintResolutionDays.ToString(), "DefaultComplaintResolutionDays", changedBy, now);
            LogChange(settings.CriticalComplaintResolutionDays.ToString(), model.CriticalComplaintResolutionDays.ToString(), "CriticalComplaintResolutionDays", changedBy, now);
            LogChange(settings.SeriousAEReportingDays.ToString(), model.SeriousAEReportingDays.ToString(), "SeriousAEReportingDays", changedBy, now);
            LogChange(settings.FatalAEReportingDays.ToString(), model.FatalAEReportingDays.ToString(), "FatalAEReportingDays", changedBy, now);
            LogChange(settings.EnforceFourEyes.ToString(), model.EnforceFourEyes.ToString(), "EnforceFourEyes", changedBy, now);
            LogChange(settings.PasswordExpiryDays.ToString(), model.PasswordExpiryDays.ToString(), "PasswordExpiryDays", changedBy, now);
            LogChange(settings.PasswordHistoryCount.ToString(), model.PasswordHistoryCount.ToString(), "PasswordHistoryCount", changedBy, now);
            LogChange(settings.IdleTimeoutMinutes.ToString(), model.IdleTimeoutMinutes.ToString(), "IdleTimeoutMinutes", changedBy, now);
            LogChange(settings.RetentionHoldCooldownDays.ToString(), model.RetentionHoldCooldownDays.ToString(), "RetentionHoldCooldownDays", changedBy, now);
            LogChange(settings.CapaRetentionYears.ToString(), model.CapaRetentionYears.ToString(), "CapaRetentionYears", changedBy, now);
            LogChange(settings.DeviationRetentionYears.ToString(), model.DeviationRetentionYears.ToString(), "DeviationRetentionYears", changedBy, now);
            LogChange(settings.ChangeRetentionYears.ToString(), model.ChangeRetentionYears.ToString(), "ChangeRetentionYears", changedBy, now);
            LogChange(settings.ComplaintRetentionYears.ToString(), model.ComplaintRetentionYears.ToString(), "ComplaintRetentionYears", changedBy, now);
            LogChange(settings.RiskRetentionYears.ToString(), model.RiskRetentionYears.ToString(), "RiskRetentionYears", changedBy, now);
            LogChange(settings.AuditRetentionYears.ToString(), model.AuditRetentionYears.ToString(), "AuditRetentionYears", changedBy, now);
            LogChange(settings.MaintenanceRetentionYears.ToString(), model.MaintenanceRetentionYears.ToString(), "MaintenanceRetentionYears", changedBy, now);
            LogChange(settings.TrainingRetentionYears.ToString(), model.TrainingRetentionYears.ToString(), "TrainingRetentionYears", changedBy, now);
            LogChange(settings.DocumentRetentionYears.ToString(), model.DocumentRetentionYears.ToString(), "DocumentRetentionYears", changedBy, now);
            LogChange(settings.RIRetentionYears.ToString(), model.RIRetentionYears.ToString(), "RIRetentionYears", changedBy, now);
            LogChange(settings.LCRetentionYears.ToString(), model.LCRetentionYears.ToString(), "LCRetentionYears", changedBy, now);

            // Compare and log email fields
            LogChange(settings.SmtpHost, model.SmtpHost, "SmtpHost", changedBy, now);
            LogChange(settings.SmtpPort.ToString(), model.SmtpPort.ToString(), "SmtpPort", changedBy, now);
            LogChange(settings.SmtpUsername, model.SmtpUsername, "SmtpUsername", changedBy, now);
            LogChange(settings.EmailFromAddress, model.EmailFromAddress, "EmailFromAddress", changedBy, now);
            LogChange(settings.EmailFromName, model.EmailFromName, "EmailFromName", changedBy, now);
            LogChange(settings.SiteWebAddress, model.SiteWebAddress, "SiteWebAddress", changedBy, now);
            LogChange(settings.SendAssignmentEmails.ToString(), model.SendAssignmentEmails.ToString(), "SendAssignmentEmails", changedBy, now);

            // Log password change without recording the actual value
            if (!string.IsNullOrWhiteSpace(model.SmtpPassword))
            {
                var oldPasswordStatus = string.IsNullOrWhiteSpace(settings.SmtpPassword) ? "not set" : "set";
                var newPasswordStatus = "set";
                // A new password is always logged as a change ("set"); the value itself is never recorded.
                    _context.SettingsAuditLog.Add(new SettingsAuditLog
                    {
                        FieldName  = "SmtpPassword",
                        OldValue   = oldPasswordStatus,
                        NewValue   = oldPasswordStatus == "set" ? "changed" : newPasswordStatus,
                        ChangedBy  = changedBy,
                        ChangedOn  = now
                    });
            }

            // Apply values
            settings.OrganisationName               = model.OrganisationName;
            settings.OrganisationAddress            = model.OrganisationAddress;
            settings.TimeZoneId                     = model.TimeZoneId;
            settings.SiteLanguage                   = model.SiteLanguage;
            settings.AllowUserLanguageChoice        = model.AllowUserLanguageChoice;
            settings.DefaultPageSize                = model.DefaultPageSize;
            settings.LcExpiryWarningDays            = model.LcExpiryWarningDays;
            settings.MaintenanceDueWarningDays      = model.MaintenanceDueWarningDays;
            settings.TrainingDueWarningDays         = model.TrainingDueWarningDays;
            settings.DefaultCapaResolutionDays      = model.DefaultCapaResolutionDays;
            settings.DefaultDeviationResolutionDays = model.DefaultDeviationResolutionDays;
            settings.DefaultComplaintResolutionDays      = model.DefaultComplaintResolutionDays;
            settings.CriticalComplaintResolutionDays     = model.CriticalComplaintResolutionDays;
            settings.SeriousAEReportingDays         = model.SeriousAEReportingDays;
            settings.FatalAEReportingDays           = model.FatalAEReportingDays;
            settings.EnforceFourEyes                = model.EnforceFourEyes;
            settings.PasswordExpiryDays             = model.PasswordExpiryDays;
            settings.PasswordHistoryCount           = model.PasswordHistoryCount;
            settings.IdleTimeoutMinutes             = model.IdleTimeoutMinutes;
            settings.RetentionHoldCooldownDays      = model.RetentionHoldCooldownDays;
            settings.CapaRetentionYears             = model.CapaRetentionYears;
            settings.DeviationRetentionYears        = model.DeviationRetentionYears;
            settings.ChangeRetentionYears           = model.ChangeRetentionYears;
            settings.ComplaintRetentionYears        = model.ComplaintRetentionYears;
            settings.RiskRetentionYears             = model.RiskRetentionYears;
            settings.AuditRetentionYears            = model.AuditRetentionYears;
            settings.MaintenanceRetentionYears      = model.MaintenanceRetentionYears;
            settings.TrainingRetentionYears         = model.TrainingRetentionYears;
            settings.DocumentRetentionYears         = model.DocumentRetentionYears;
            settings.RIRetentionYears               = model.RIRetentionYears;
            settings.LCRetentionYears               = model.LCRetentionYears;
            settings.SmtpHost                       = model.SmtpHost;
            settings.SmtpPort                       = model.SmtpPort;
            settings.SmtpUsername                   = model.SmtpUsername;
            settings.EmailFromAddress               = model.EmailFromAddress;
            settings.EmailFromName                  = model.EmailFromName;
            settings.SiteWebAddress                 = model.SiteWebAddress;
            settings.SendAssignmentEmails           = model.SendAssignmentEmails;
            // Only update password if a new one was provided — blank means "keep existing". Stored protected.
            if (!string.IsNullOrWhiteSpace(model.SmtpPassword))
                settings.SmtpPassword = _smtpSecret.Protect(model.SmtpPassword);

            await _context.SaveChangesAsync();
            SiteTime.Configure(settings.TimeZoneId);

            TempData["Success"] = this.T("Settings saved successfully.");
            return RedirectToAction(nameof(Index));
        }

        // POST: Settings/SendTestEmail — sends one message now, with the SAVED settings, to the Administrator's own address.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> SendTestEmail()
        {
            var settings = await GetOrCreateSettingsAsync();
            var user = await HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>().GetUserAsync(User);
            var to = user?.Email;
            string result;
            if (string.IsNullOrWhiteSpace(to))
            {
                TempData["Error"] = this.T("Your account has no e-mail address.");
                return RedirectToAction(nameof(Index));
            }
            try
            {
                await _smtp.SendAsync(settings, to, "MintQMS test e-mail",
                    "<p>This is a test e-mail from MintQMS. If you received it, the e-mail settings work.</p>");
                TempData["Success"] = this.T("Test e-mail sent to {0}. Check that it arrived.", to);
                result = "sent";
            }
            catch (Exception ex)
            {
                TempData["Error"] = this.T("The test e-mail could not be sent: {0}", ex.Message);
                result = "failed: " + (ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
            }
            _context.SettingsAuditLog.Add(new SettingsAuditLog
            {
                FieldName = "TestEmail", OldValue = null, NewValue = $"to {to}: {result}",
                ChangedBy = User.Identity?.Name ?? "System", ChangedOn = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task SetOutboxStatusAsync()
        {
            ViewBag.OutboxPending = await _context.EmailOutbox.CountAsync(m => m.Status == EmailOutboxStatus.Pending);
            ViewBag.OutboxFailed = await _context.EmailOutbox.CountAsync(m => m.Status == EmailOutboxStatus.Failed);
            ViewBag.OutboxSent = await _context.EmailOutbox.CountAsync(m => m.Status == EmailOutboxStatus.Sent);
            ViewBag.OutboxLastError = await _context.EmailOutbox.Where(m => m.LastError != null)
                .OrderByDescending(m => m.Id).AsNoTracking().FirstOrDefaultAsync();
        }

        // GET: Settings/AuditLog
        public async Task<IActionResult> AuditLog()
        {
            var logs = await _context.SettingsAuditLog
                .OrderByDescending(l => l.ChangedOn)
                .ToListAsync();
            return View(logs);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private async Task<SystemSettings> GetOrCreateSettingsAsync()
        {
            var settings = await _context.SystemSettings.FindAsync(1);
            if (settings != null) return settings;

            settings = new SystemSettings { Id = 1 };
            _context.SystemSettings.Add(settings);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request inserted the row first — detach and reload
                _context.Entry(settings).State = EntityState.Detached;
                settings = await _context.SystemSettings.FindAsync(1)
                    ?? throw new InvalidOperationException("SystemSettings row could not be initialised.");
            }
            return settings;
        }

        private BackupStatusEntry? ReadBackupStatus()
        {
            try
            {
                if (!System.IO.File.Exists(_backupStatusFile)) return null;
                var json = System.IO.File.ReadAllText(_backupStatusFile);
                return JsonSerializer.Deserialize<BackupStatusEntry>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private void LogChange(string? oldValue, string? newValue, string fieldName, string changedBy, DateTime changedOn)
        {
            if (string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
                return;

            _context.SettingsAuditLog.Add(new SettingsAuditLog
            {
                FieldName  = fieldName,
                OldValue   = oldValue,
                NewValue   = newValue,
                ChangedBy  = changedBy,
                ChangedOn  = changedOn
            });
        }
    }
}
