using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.LC;

namespace OpenQMS.Services
{
    /// <summary>
    /// Validates business rules and executes FSM status transitions for LicCert records.
    /// </summary>
    public class LicCertWorkflowService
    {
        private readonly ApplicationDbContext _context;

        public LicCertWorkflowService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── Result type ────────────────────────────────────────────────────────

        public class WorkflowResult
        {
            public bool    Success { get; init; }
            public string? Error   { get; init; }

            public static WorkflowResult Ok()               => new() { Success = true };
            public static WorkflowResult Fail(string error) => new() { Success = false, Error = error };
        }

        // ── FSM Transition Table ───────────────────────────────────────────────

        private static readonly HashSet<(LCStatus From, LCStatus To)> ValidTransitions = new()
        {
            // Draft paths
            (LCStatus.Draft,            LCStatus.Applied),
            (LCStatus.Draft,            LCStatus.NotApplicable),

            // Applied paths
            (LCStatus.Applied,          LCStatus.Draft),           // revert
            (LCStatus.Applied,          LCStatus.UnderAssessment),
            (LCStatus.Applied,          LCStatus.Withdrawn),       // PASSWORD

            // Under Assessment paths
            (LCStatus.UnderAssessment,  LCStatus.Applied),         // revert
            (LCStatus.UnderAssessment,  LCStatus.IssuedActive),    // PASSWORD
            (LCStatus.UnderAssessment,  LCStatus.Suspended),       // PASSWORD
            (LCStatus.UnderAssessment,  LCStatus.Withdrawn),       // PASSWORD

            // Issued-Active paths
            (LCStatus.IssuedActive,     LCStatus.RenewalDue),        // manual (Initiate Renewal Now)
            (LCStatus.IssuedActive,     LCStatus.Expired),            // manual (Mark Expired)
            (LCStatus.IssuedActive,     LCStatus.VariationRequired),  // post-issue change
            (LCStatus.IssuedActive,     LCStatus.Suspended),          // PASSWORD
            (LCStatus.IssuedActive,     LCStatus.Withdrawn),          // PASSWORD

            // Variation paths (post-issue change; record unlocked for editing)
            (LCStatus.VariationRequired, LCStatus.UnderAssessment),   // submit variation for assessment
            (LCStatus.VariationRequired, LCStatus.IssuedActive),      // apply variation / cancel back

            // Renewal Due paths
            (LCStatus.RenewalDue,       LCStatus.Applied),          // renewal submission
            (LCStatus.RenewalDue,       LCStatus.Expired),           // system
            (LCStatus.RenewalDue,       LCStatus.Suspended),         // PASSWORD

            // Terminal recovery paths
            (LCStatus.Expired,          LCStatus.Applied),           // restart
            (LCStatus.Suspended,        LCStatus.Applied),           // reinstatement
            (LCStatus.Withdrawn,        LCStatus.Draft),              // reopen

            // Not Applicable reversal
            (LCStatus.NotApplicable,    LCStatus.Draft),
        };

        // ── Closed-state check ────────────────────────────────────────────────
        // Adverse end states whose data must be frozen (immutable). Recovery is
        // by an explicit workflow transition (restart / reinstate / reopen), not
        // by editing in place — so locking these does not trap the record. This
        // is distinct from a live record locked for Variation control.
        public static bool IsClosed(LCStatus status)
            => status is LCStatus.Expired or LCStatus.Suspended or LCStatus.Withdrawn;

        // ── Validate ──────────────────────────────────────────────────────────

        public WorkflowResult ValidateTransition(
            LicenceCertificationRecord record,
            LCStatus newStatus,
            string? reason = null)
        {
            if (!ValidTransitions.Contains((record.Status, newStatus)))
                return WorkflowResult.Fail($"Cannot transition from '{record.Status}' to '{newStatus}'.");

            // Justification/reason required for certain transitions
            if (newStatus == LCStatus.Withdrawn && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A withdrawal reason is required.");

            if (newStatus == LCStatus.Suspended && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A suspension reason is required.");

            if (newStatus == LCStatus.NotApplicable && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A justification is required when marking a record as Not Applicable.");

            // IssuedActive requires LicenceNumber and IssueDate at minimum
            if (newStatus == LCStatus.IssuedActive)
            {
                if (string.IsNullOrWhiteSpace(record.LicenceNumber))
                    return WorkflowResult.Fail("A Licence / Certificate Number must be recorded before issuing.");
                if (!record.IssueDate.HasValue)
                    return WorkflowResult.Fail("An Issue Date must be recorded before issuing.");
            }

            return WorkflowResult.Ok();
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        public async Task ApplyTransition(
            LicenceCertificationRecord record,
            LCStatus newStatus,
            string performedByUserId,
            string performedByUserName,
            string? reason = null)
        {
            var oldStatus = record.Status;
            record.Status     = newStatus;
            record.ModifiedBy = performedByUserName;
            record.ModifiedAt = DateTime.UtcNow;

            // Status-specific side effects
            switch (newStatus)
            {
                case LCStatus.Applied:
                    record.ApplicationDate ??= SiteTime.Today;
                    record.IsLocked = false;
                    break;

                case LCStatus.IssuedActive:
                {
                    record.IssueDate ??= SiteTime.Today;
                    // Issued / Active is a final, granted state: lock the record so its
                    // documents and attachments can no longer be uploaded or deleted.
                    // The renewal cycle re-opens the record by transitioning it to
                    // RenewalDue (which unlocks it) before returning here.
                    record.IsLocked = true;
                    record.RenewalCycle++;

                    var docs = await _context.LCDocuments
                        .Where(d => d.LicenceCertificationRecordId == record.Id && !d.IsDeleted && d.ApprovalCycle == null)
                        .ToListAsync();
                    foreach (var d in docs) d.ApprovalCycle = record.RenewalCycle;

                    // Resolve display names for navigation/foreign-key fields so the
                    // snapshot is self-contained and human-readable.
                    var licenceTypeName = await _context.LCLicenceTypes
                        .Where(t => t.Id == record.LicenceTypeId)
                        .Select(t => t.DisplayName)
                        .FirstOrDefaultAsync();

                    string? responsiblePersonName = null;
                    if (int.TryParse(record.InternalResponsiblePersonId, out var rpId))
                        responsiblePersonName = await _context.Users
                            .Where(u => u.Id == rpId)
                            .Select(u => (u.FirstName + " " + u.LastName).Trim())
                            .FirstOrDefaultAsync();
                    responsiblePersonName ??= record.InternalResponsiblePersonId;

                    _context.LCApprovalSnapshots.Add(new LCApprovalSnapshot
                    {
                        LicenceCertificationRecordId = record.Id,
                        RenewalCycle  = record.RenewalCycle,
                        TakenAt       = DateTime.UtcNow,
                        TakenByUserId = performedByUserId,
                        SnapshotJson  = JsonSerializer.Serialize(new
                        {
                            // ── Identification ──
                            record.LCNumber,
                            HolderType        = record.HolderType.ToString(),
                            record.HolderName,
                            record.LinkedSiteAddress,
                            record.LinkedSiteCountry,
                            // ── Classification ──
                            Category          = record.Category.ToString(),
                            LicenceType       = licenceTypeName,
                            record.IssuingBody,
                            record.IssuingBodyUrl,
                            record.IssuingBodyPortal,
                            // ── Licence Details ──
                            record.LicenceNumber,
                            record.Scope,
                            ApplicationDate   = record.ApplicationDate?.ToString("yyyy-MM-dd"),
                            IssueDate         = record.IssueDate?.ToString("yyyy-MM-dd"),
                            ExpiryDate        = record.ExpiryDate?.ToString("yyyy-MM-dd"),
                            NextRenewalDue    = record.NextRenewalDue?.ToString("yyyy-MM-dd"),
                            record.RenewalLeadTimeDays,
                            // ── GMP Clearance Specifics ──
                            GmcPathway        = record.GmcPathway?.ToString(),
                            record.SiteCountry,
                            record.ManufacturingStepsScope,
                            LastInspectionDate        = record.LastInspectionDate?.ToString("yyyy-MM-dd"),
                            record.InspectionAuthority,
                            RFIDate                   = record.RFIDate?.ToString("yyyy-MM-dd"),
                            RFIResponseDueDate        = record.RFIResponseDueDate?.ToString("yyyy-MM-dd"),
                            RFIResponseSubmittedDate  = record.RFIResponseSubmittedDate?.ToString("yyyy-MM-dd"),
                            LetterOfAccessAvailable   = record.LetterOfAccessAvailable ? "Yes" : null,
                            // ── ISO / Certification Specifics ──
                            record.CertificationBody,
                            record.CertificationBodyAccreditation,
                            record.StandardVersion,
                            record.CertificateScope,
                            Stage1AuditDate           = record.Stage1AuditDate?.ToString("yyyy-MM-dd"),
                            Stage2AuditDate           = record.Stage2AuditDate?.ToString("yyyy-MM-dd"),
                            NextSurveillanceAuditDate = record.NextSurveillanceAuditDate?.ToString("yyyy-MM-dd"),
                            RecertificationDueDate    = record.RecertificationDueDate?.ToString("yyyy-MM-dd"),
                            // ── Contacts ──
                            InternalResponsiblePerson = responsiblePersonName,
                            record.ExternalConsultant,
                            record.AuthorityContactEmail,
                            record.AuthorityPortalUsername,
                            // ── Fees ──
                            ApplicationFee = record.ApplicationFee?.ToString("0.00"),
                            FeeCurrency    = record.ApplicationFee.HasValue ? record.FeeCurrency : null,
                            FeePaidDate    = record.FeePaidDate?.ToString("yyyy-MM-dd"),
                            record.FeeReference,
                            // ── Notes ──
                            record.JustificationReason,
                            record.RegulatoryActionNotes,
                            // ── Related records ──
                            Conditions = record.Conditions
                                .Where(c => !c.IsDeleted)
                                .Select(c => new { c.ConditionDescription, Status = c.Status.ToString() })
                                .ToList(),
                            Documents = docs
                                .Select(d => new { d.FileName, Type = d.DocumentType.ToString(), d.FileHash, d.StoragePath })
                                .ToList()
                        })
                    });
                    break;
                }

                case LCStatus.ConditionsApplied:
                    // Active but carrying outstanding post-issue conditions — still a
                    // granted/final state, so keep it locked against document changes.
                    record.IsLocked = true;
                    break;

                case LCStatus.RenewalDue:
                    record.IsLocked = false;
                    break;

                case LCStatus.Expired:
                    record.IsLocked = true;
                    if (record.ExpiryDate.HasValue) { var s = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings(); record.RetainUntil = record.ExpiryDate.Value.AddYears(s.LCRetentionYears); }
                    break;

                case LCStatus.Suspended:
                    record.JustificationReason = reason;
                    record.SuspendedOn = SiteTime.Today;
                    record.IsLocked = true;
                    { var s = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings(); record.RetainUntil = record.SuspendedOn.Value.AddYears(s.LCRetentionYears); }
                    break;

                case LCStatus.Withdrawn:
                    record.JustificationReason = reason;
                    record.WithdrawnOn = SiteTime.Today;
                    record.IsLocked = true;
                    { var s = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings(); record.RetainUntil = record.WithdrawnOn.Value.AddYears(s.LCRetentionYears); }
                    break;

                case LCStatus.NotApplicable:
                    record.JustificationReason = reason;
                    record.IsLocked = false;
                    break;

                case LCStatus.VariationRequired:
                    // Post-issue change in progress — unlock so the record can be edited.
                    record.IsLocked = false;
                    break;

                case LCStatus.Draft:
                    record.IsLocked = false;
                    break;

                case LCStatus.UnderAssessment:
                    record.IsLocked = false;
                    break;
            }

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = record.Id,
                Action    = $"Status changed from {oldStatus} to {newStatus}",
                Details   = reason,
                UserId    = performedByUserId,
                UserName  = performedByUserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        /// <summary>Returns the set of statuses this record can validly transition to.</summary>
        public static IEnumerable<LCStatus> GetAvailableTransitions(LCStatus current)
            => ValidTransitions
               .Where(t => t.From == current)
               .Select(t => t.To);

        /// <summary>Returns true if this transition requires password authentication.</summary>
        public static bool RequiresPassword(LCStatus to)
            => to == LCStatus.IssuedActive
            || to == LCStatus.Suspended
            || to == LCStatus.Withdrawn;
    }
}
