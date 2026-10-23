using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.ViewModels;

namespace OpenQMS.Services
{
    // Gap 13: Encapsulates all data-retention business logic.
    public class RetentionService
    {
        private readonly ApplicationDbContext _context;

        public RetentionService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Calculate RetainUntil for a given module based on the terminal date and settings.
        public DateTime CalculateRetainUntil(string module, DateTime terminalDate, SystemSettings s)
        {
            int years = module switch
            {
                "CAPA"        => s.CapaRetentionYears,
                "Deviation"   => s.DeviationRetentionYears,
                "Change"      => s.ChangeRetentionYears,
                "Complaint"   => s.ComplaintRetentionYears,
                "Risk"        => s.RiskRetentionYears,
                "Audit"       => s.AuditRetentionYears,
                "Maintenance" => s.MaintenanceRetentionYears,
                "Training"    => s.TrainingRetentionYears,
                "Document"    => s.DocumentRetentionYears,
                "RI"          => s.RIRetentionYears,
                "LC"          => s.LCRetentionYears,
                _             => 5
            };
            return terminalDate.AddYears(years);
        }

        // Load the single SystemSettings row (Id = 1).
        public async Task<SystemSettings> GetSettingsAsync()
            => await _context.SystemSettings.FindAsync(1)
               ?? new SystemSettings();

        // Returns a flat list of all disposal-eligible records across all 11 modules.
        // Eligible = not already deleted, RetainUntil is set and in the past.
        // Includes Retention Extended records (shown but not deletable).
        public async Task<List<DisposalEligibleItem>> GetEligibleItemsAsync()
        {
            var now = DateTime.UtcNow;
            var items = new List<DisposalEligibleItem>();

            // CAPAs
            var capas = await _context.Capa
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "CAPA",
                    RecordId         = x.Id,
                    RecordReference  = x.CapaId,
                    Title            = x.Title,
                    TerminalStatus   = "Approved",
                    TerminalDate     = x.ApprovedOn!.Value,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(capas);

            // Deviations
            var devs = await _context.Deviation
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "Deviation",
                    RecordId         = x.Id,
                    RecordReference  = x.DeviationId,
                    Title            = x.Title,
                    TerminalStatus   = "Approved",
                    TerminalDate     = x.ApprovedOn!.Value,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(devs);

            // Changes
            var changes = await _context.Change
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "Change",
                    RecordId         = x.Id,
                    RecordReference  = x.ChangeId,
                    Title            = x.Title,
                    TerminalStatus   = "Approved",
                    TerminalDate     = x.ApprovedOn!.Value,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(changes);

            // Complaints
            var complaints = await _context.Complaint
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "Complaint",
                    RecordId         = x.Id,
                    RecordReference  = x.ComplaintId,
                    Title            = x.Title,
                    TerminalStatus   = "Closed",
                    TerminalDate     = x.ClosedOn!.Value,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(complaints);

            // Risks
            var risks = await _context.Risk
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "Risk",
                    RecordId         = x.Id,
                    RecordReference  = x.RiskId,
                    Title            = x.Title,
                    TerminalStatus   = "Closed",
                    TerminalDate     = x.ClosedOn!.Value,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(risks);

            // Audits — materialize first to avoid int.ToString() translation failure
            var auditRows = await _context.Audit
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .ToListAsync();
            items.AddRange(auditRows.Select(x => new DisposalEligibleItem
            {
                Module           = "Audit",
                RecordId         = x.Id,
                RecordReference  = $"AUD-{x.Id}",
                Title            = x.Title,
                TerminalStatus   = "Closed",
                TerminalDate     = x.ClosedOn!.Value,
                RetainUntil      = x.RetainUntil!.Value,
                RetentionExtended          = x.RetentionExtended,
                RetentionExtendedReason    = x.RetentionExtendedReason,
                RetentionExtendedComments  = x.RetentionExtendedComments,
                RetentionExtendedBy        = x.RetentionExtendedBy,
                RetentionExtendedOn        = x.RetentionExtendedOn,
                RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
            }));

            // Maintenances — materialize first to avoid int.ToString() translation failure
            var maintRows = await _context.Maintenance
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .ToListAsync();
            items.AddRange(maintRows.Select(x => new DisposalEligibleItem
            {
                Module           = "Maintenance",
                RecordId         = x.Id,
                RecordReference  = $"MNT-{x.Id}",
                Title            = x.Title,
                TerminalStatus   = "Approved",
                TerminalDate     = x.ApprovedOn!.Value,
                RetainUntil      = x.RetainUntil!.Value,
                RetentionExtended          = x.RetentionExtended,
                RetentionExtendedReason    = x.RetentionExtendedReason,
                RetentionExtendedComments  = x.RetentionExtendedComments,
                RetentionExtendedBy        = x.RetentionExtendedBy,
                RetentionExtendedOn        = x.RetentionExtendedOn,
                RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
            }));

            // Trainings
            var trainings = await _context.Training
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "Training",
                    RecordId         = x.Id,
                    RecordReference  = x.TrainingId,
                    Title            = x.Name,
                    TerminalStatus   = "Completed",
                    TerminalDate     = x.CompletedOn!.Value,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(trainings);

            // Documents
            var docs = await _context.AppDocument
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "Document",
                    RecordId         = x.Id,
                    RecordReference  = x.DocumentId,
                    Title            = x.Title,
                    TerminalStatus   = "Obsolete",
                    TerminalDate     = x.ObsoletedOn!.Value,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(docs);

            // Regulatory Applications
            var ris = await _context.RegulatoryApplications
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "RI",
                    RecordId         = x.Id,
                    RecordReference  = x.RINumber,
                    Title            = x.ProductName,
                    TerminalStatus   = x.Status.ToString(),
                    TerminalDate     = x.ApprovalDate ?? x.ActualDecisionDate ?? x.WithdrawnOn ?? x.CreatedAt,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(ris);

            // Licence/Certification Records
            var lcs = await _context.LicenceCertificationRecords
                .Where(x => !x.IsDeleted && x.RetainUntil.HasValue && x.RetainUntil <= now)
                .Select(x => new DisposalEligibleItem
                {
                    Module           = "LC",
                    RecordId         = x.Id,
                    RecordReference  = x.LCNumber,
                    Title            = x.HolderName,
                    TerminalStatus   = x.Status.ToString(),
                    TerminalDate     = x.ExpiryDate ?? x.SuspendedOn ?? x.WithdrawnOn ?? x.CreatedAt,
                    RetainUntil      = x.RetainUntil!.Value,
                    RetentionExtended          = x.RetentionExtended,
                    RetentionExtendedReason    = x.RetentionExtendedReason,
                    RetentionExtendedComments  = x.RetentionExtendedComments,
                    RetentionExtendedBy        = x.RetentionExtendedBy,
                    RetentionExtendedOn        = x.RetentionExtendedOn,
                    RetentionHoldCooldownUntil = x.RetentionHoldCooldownUntil
                }).ToListAsync();
            items.AddRange(lcs);

            return items.OrderBy(i => i.RetainUntil).ToList();
        }
    }
}
