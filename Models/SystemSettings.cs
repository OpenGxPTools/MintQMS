using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class SystemSettings
    {
        public int Id { get; set; }

        // ── Tier 1: Organisation Profile ─────────────────────────────────
        [Display(Name = "Organisation Name")]
        [MaxLength(200)]
        public string OrganisationName { get; set; } = string.Empty;

        [Display(Name = "Organisation Address")]
        [MaxLength(500)]
        public string OrganisationAddress { get; set; } = string.Empty;

        [Display(Name = "Logo Path")]
        [MaxLength(500)]
        public string? LogoPath { get; set; }

        [Display(Name = "Date Format")]
        [MaxLength(20)]
        public string DateFormat { get; set; } = "dd/MM/yyyy";

        [Display(Name = "Site Time Zone")]
        [MaxLength(64)]
        public string TimeZoneId { get; set; } = "Australia/Brisbane";

        [Display(Name = "Default Page Size")]
        [Range(5, 200)]
        public int DefaultPageSize { get; set; } = 25;

        // ── Tier 2: Expiry / Due Warning Thresholds ───────────────────────
        [Display(Name = "LC Expiry Warning (days)")]
        [Range(1, 365)]
        public int LcExpiryWarningDays { get; set; } = 90;

        [Display(Name = "Maintenance Due Warning (days)")]
        [Range(1, 365)]
        public int MaintenanceDueWarningDays { get; set; } = 30;

        [Display(Name = "Training Due Warning (days)")]
        [Range(1, 365)]
        public int TrainingDueWarningDays { get; set; } = 30;

        // ── Tier 2: Default Resolution Deadlines ─────────────────────────
        [Display(Name = "Default CAPA Resolution (days)")]
        [Range(1, 365)]
        public int DefaultCapaResolutionDays { get; set; } = 30;

        [Display(Name = "Default Deviation Resolution (days)")]
        [Range(1, 365)]
        public int DefaultDeviationResolutionDays { get; set; } = 30;

        [Display(Name = "Default Complaint Resolution (days)")]
        [Range(1, 365)]
        public int DefaultComplaintResolutionDays { get; set; } = 30;

        [Display(Name = "Critical Complaint Resolution (days)")]
        [Range(1, 90)]
        public int CriticalComplaintResolutionDays { get; set; } = 7;

        // ── Security Policy ───────────────────────────────────────────────
        [Display(Name = "Password Expiry (days)")]
        [Range(0, 365)]
        public int PasswordExpiryDays { get; set; } = 90;   // 0 = disabled

        [Display(Name = "Password History Count")]
        [Range(0, 24)]
        public int PasswordHistoryCount { get; set; } = 5;  // 0 = disabled

        [Display(Name = "Idle Session Timeout (minutes)")]
        [Range(0, 480)]
        public int IdleTimeoutMinutes { get; set; } = 15;   // 0 = disabled

        // ── Email Configuration ────────────────────────────────────────────
        [Display(Name = "SMTP Host")]
        [MaxLength(200)]
        public string? SmtpHost { get; set; }

        [Display(Name = "SMTP Port")]
        [Range(1, 65535)]
        public int SmtpPort { get; set; } = 587;

        [Display(Name = "SMTP Username")]
        [MaxLength(200)]
        public string? SmtpUsername { get; set; }

        [Display(Name = "SMTP Password")]
        [MaxLength(500)]
        public string? SmtpPassword { get; set; }

        [Display(Name = "From Address")]
        [MaxLength(200)]
        [EmailAddress]
        public string? EmailFromAddress { get; set; }

        [Display(Name = "From Name")]
        [MaxLength(200)]
        public string? EmailFromName { get; set; }

        // ── Gap 13: Data Retention Periods ────────────────────────────────────
        [Display(Name = "Retention Hold Cooldown (days)")]
        [Range(1, 365)]
        public int RetentionHoldCooldownDays { get; set; } = 30;

        [Display(Name = "CAPA Retention (years)")]
        [Range(1, 50)]
        public int CapaRetentionYears { get; set; } = 5;

        [Display(Name = "Deviation Retention (years)")]
        [Range(1, 50)]
        public int DeviationRetentionYears { get; set; } = 5;

        [Display(Name = "Change Retention (years)")]
        [Range(1, 50)]
        public int ChangeRetentionYears { get; set; } = 5;

        [Display(Name = "Complaint Retention (years)")]
        [Range(1, 50)]
        public int ComplaintRetentionYears { get; set; } = 5;

        [Display(Name = "Risk Retention (years)")]
        [Range(1, 50)]
        public int RiskRetentionYears { get; set; } = 5;

        [Display(Name = "Audit Retention (years)")]
        [Range(1, 50)]
        public int AuditRetentionYears { get; set; } = 5;

        [Display(Name = "Maintenance Retention (years)")]
        [Range(1, 50)]
        public int MaintenanceRetentionYears { get; set; } = 5;

        [Display(Name = "Training Retention (years)")]
        [Range(1, 50)]
        public int TrainingRetentionYears { get; set; } = 5;

        [Display(Name = "Document Retention (years)")]
        [Range(1, 50)]
        public int DocumentRetentionYears { get; set; } = 5;

        [Display(Name = "Document Periodic Review Period (months)")]
        [Range(1, 120)]
        public int DocumentReviewPeriodMonths { get; set; } = 24;

        [Display(Name = "Regulatory Application Retention (years)")]
        [Range(1, 50)]
        public int RIRetentionYears { get; set; } = 10;

        [Display(Name = "Licence/Certification Retention (years)")]
        [Range(1, 50)]
        public int LCRetentionYears { get; set; } = 10;
    }
}
