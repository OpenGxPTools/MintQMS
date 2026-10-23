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

        private static readonly string[] AllowedLogoExtensions = { ".png", ".jpg", ".jpeg", ".gif" };
        private const long MaxLogoBytes = 2 * 1024 * 1024; // 2 MB

        public SettingsController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _backupStatusFile = configuration["BackupStatusFile"] ?? "/var/backups/mintqms/backup_status.json";
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            var settings = await GetOrCreateSettingsAsync();
            ViewBag.CanEdit = User.IsInRole(Constants.AdministratorsRole);
            ViewBag.BackupStatus = ReadBackupStatus();
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

            if (!SiteTime.IsValid(model.TimeZoneId))
                ModelState.AddModelError(nameof(SystemSettings.TimeZoneId), "Unknown time zone.");

            if (!ModelState.IsValid)
            {
                ViewBag.CanEdit = true;
                model.LogoPath = settings.LogoPath; // preserve current logo on re-render
                return View("Index", model);
            }

            // Handle logo upload
            if (logoFile != null && logoFile.Length > 0)
            {
                var ext = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
                if (!AllowedLogoExtensions.Contains(ext))
                {
                    ModelState.AddModelError("logoFile", "Only PNG, JPG, or GIF files are allowed.");
                    ViewBag.CanEdit = true;
                    model.LogoPath = settings.LogoPath;
                    return View("Index", model);
                }
                if (logoFile.Length > MaxLogoBytes)
                {
                    ModelState.AddModelError("logoFile", "Logo must be 2 MB or smaller.");
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
            LogChange(settings.DateFormat, model.DateFormat, "DateFormat", changedBy, now);
            LogChange(settings.TimeZoneId, model.TimeZoneId, "TimeZoneId", changedBy, now);
            LogChange(settings.DefaultPageSize.ToString(), model.DefaultPageSize.ToString(), "DefaultPageSize", changedBy, now);
            LogChange(settings.LcExpiryWarningDays.ToString(), model.LcExpiryWarningDays.ToString(), "LcExpiryWarningDays", changedBy, now);
            LogChange(settings.MaintenanceDueWarningDays.ToString(), model.MaintenanceDueWarningDays.ToString(), "MaintenanceDueWarningDays", changedBy, now);
            LogChange(settings.TrainingDueWarningDays.ToString(), model.TrainingDueWarningDays.ToString(), "TrainingDueWarningDays", changedBy, now);
            LogChange(settings.DefaultCapaResolutionDays.ToString(), model.DefaultCapaResolutionDays.ToString(), "DefaultCapaResolutionDays", changedBy, now);
            LogChange(settings.DefaultDeviationResolutionDays.ToString(), model.DefaultDeviationResolutionDays.ToString(), "DefaultDeviationResolutionDays", changedBy, now);
            LogChange(settings.DefaultComplaintResolutionDays.ToString(), model.DefaultComplaintResolutionDays.ToString(), "DefaultComplaintResolutionDays", changedBy, now);
            LogChange(settings.CriticalComplaintResolutionDays.ToString(), model.CriticalComplaintResolutionDays.ToString(), "CriticalComplaintResolutionDays", changedBy, now);
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

            // Log password change without recording the actual value
            if (!string.IsNullOrWhiteSpace(model.SmtpPassword))
            {
                var oldPasswordStatus = string.IsNullOrWhiteSpace(settings.SmtpPassword) ? "not set" : "set";
                var newPasswordStatus = "set";
                if (oldPasswordStatus != newPasswordStatus || settings.SmtpPassword != model.SmtpPassword)
                    _context.SettingsAuditLog.Add(new SettingsAuditLog
                    {
                        FieldName  = "SmtpPassword",
                        OldValue   = oldPasswordStatus,
                        NewValue   = newPasswordStatus,
                        ChangedBy  = changedBy,
                        ChangedOn  = now
                    });
            }

            // Apply values
            settings.OrganisationName               = model.OrganisationName;
            settings.OrganisationAddress            = model.OrganisationAddress;
            settings.DateFormat                     = model.DateFormat;
            settings.TimeZoneId                     = model.TimeZoneId;
            settings.DefaultPageSize                = model.DefaultPageSize;
            settings.LcExpiryWarningDays            = model.LcExpiryWarningDays;
            settings.MaintenanceDueWarningDays      = model.MaintenanceDueWarningDays;
            settings.TrainingDueWarningDays         = model.TrainingDueWarningDays;
            settings.DefaultCapaResolutionDays      = model.DefaultCapaResolutionDays;
            settings.DefaultDeviationResolutionDays = model.DefaultDeviationResolutionDays;
            settings.DefaultComplaintResolutionDays      = model.DefaultComplaintResolutionDays;
            settings.CriticalComplaintResolutionDays     = model.CriticalComplaintResolutionDays;
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
            // Only update password if a new one was provided — blank means "keep existing"
            if (!string.IsNullOrWhiteSpace(model.SmtpPassword))
                settings.SmtpPassword = model.SmtpPassword;

            await _context.SaveChangesAsync();
            SiteTime.Configure(settings.TimeZoneId);

            TempData["Success"] = "Settings saved successfully.";
            return RedirectToAction(nameof(Index));
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
