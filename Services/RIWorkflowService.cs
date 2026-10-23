using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.RI;

namespace OpenQMS.Services
{
    /// <summary>
    /// Validates business rules and executes FSM status transitions for RegIntel applications.
    /// </summary>
    public class RIWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public RIWorkflowService(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context     = context;
            _userManager = userManager;
        }

        public class WorkflowResult
        {
            public bool    Success { get; init; }
            public string? Error   { get; init; }

            public static WorkflowResult Ok()               => new() { Success = true };
            public static WorkflowResult Fail(string error) => new() { Success = false, Error = error };
        }

        // ── FSM Transition Table ──────────────────────────────────────────────

        private static readonly HashSet<(RIApplicationStatus From, RIApplicationStatus To)> ValidTransitions = new()
        {
            (RIApplicationStatus.Strategy,          RIApplicationStatus.Preparation),
            (RIApplicationStatus.Preparation,       RIApplicationStatus.Strategy),
            (RIApplicationStatus.Preparation,       RIApplicationStatus.Submitted),
            (RIApplicationStatus.Submitted,         RIApplicationStatus.Preparation),
            (RIApplicationStatus.Submitted,         RIApplicationStatus.UnderReview),
            (RIApplicationStatus.Submitted,         RIApplicationStatus.Withdrawn),
            (RIApplicationStatus.UnderReview,       RIApplicationStatus.ClockStop),
            (RIApplicationStatus.UnderReview,       RIApplicationStatus.Approved),
            (RIApplicationStatus.UnderReview,       RIApplicationStatus.ConditionsActive),
            (RIApplicationStatus.UnderReview,       RIApplicationStatus.Rejected),
            (RIApplicationStatus.UnderReview,       RIApplicationStatus.Withdrawn),
            (RIApplicationStatus.ClockStop,         RIApplicationStatus.UnderReview),
            (RIApplicationStatus.Approved,          RIApplicationStatus.VariationRequired),
            (RIApplicationStatus.Approved,          RIApplicationStatus.RenewalDue),
            (RIApplicationStatus.Approved,          RIApplicationStatus.Withdrawn),   // decided not to renew
            (RIApplicationStatus.Approved,          RIApplicationStatus.Suspended),   // authority suspension
            (RIApplicationStatus.ConditionsActive,  RIApplicationStatus.Approved),
            (RIApplicationStatus.ConditionsActive,  RIApplicationStatus.Withdrawn),
            (RIApplicationStatus.ConditionsActive,  RIApplicationStatus.Suspended),
            (RIApplicationStatus.VariationRequired, RIApplicationStatus.UnderReview),
            (RIApplicationStatus.VariationRequired, RIApplicationStatus.Approved),
            (RIApplicationStatus.RenewalDue,        RIApplicationStatus.Submitted),
            (RIApplicationStatus.RenewalDue,        RIApplicationStatus.Withdrawn),
            (RIApplicationStatus.Suspended,         RIApplicationStatus.UnderReview), // reinstatement (re-review)
            (RIApplicationStatus.Suspended,         RIApplicationStatus.Withdrawn),   // abandon while suspended
        };

        // ── Terminal-state check ──────────────────────────────────────────────
        // Closed end states have no outgoing transitions: the record is finished
        // and its data must be frozen (immutable) for audit/retention integrity.
        // This is distinct from IsLocked, which marks a *live* authorisation whose
        // changes must be routed through a Variation.
        public static bool IsTerminal(RIApplicationStatus status)
            => status is RIApplicationStatus.Rejected or RIApplicationStatus.Withdrawn;

        // ── Validate ─────────────────────────────────────────────────────────

        public WorkflowResult ValidateTransition(
            RegulatoryApplication app,
            RIApplicationStatus newStatus,
            string? reason = null)
        {
            if (!ValidTransitions.Contains((app.Status, newStatus)))
                return WorkflowResult.Fail($"Cannot transition from '{app.Status}' to '{newStatus}'.");

            // Locked records can only move through approved-state lifecycle actions:
            // raise a variation, initiate renewal, suspend, withdraw, or reinstate (re-review).
            // (The FSM table above already constrains which of these are legal per source state.)
            if (app.IsLocked &&
                newStatus is not (RIApplicationStatus.VariationRequired or
                                  RIApplicationStatus.RenewalDue or
                                  RIApplicationStatus.Suspended or
                                  RIApplicationStatus.Withdrawn or
                                  RIApplicationStatus.UnderReview))
                return WorkflowResult.Fail("This record is locked. Permitted actions: Variation, Renewal, Suspend, Withdraw, or reinstatement.");

            // Reason required for terminal/adverse decisions
            if (newStatus == RIApplicationStatus.Rejected && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A rejection reason is required.");

            if (newStatus == RIApplicationStatus.Withdrawn && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A withdrawal reason is required.");

            if (newStatus == RIApplicationStatus.Suspended && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A suspension reason is required.");

            // To approve from ConditionsActive all conditions must be closed
            if (app.Status == RIApplicationStatus.ConditionsActive &&
                newStatus == RIApplicationStatus.Approved)
            {
                var openConditions = app.Conditions?.Count(c =>
                    c.Status != RIConditionStatus.Closed &&
                    c.Status != RIConditionStatus.Waived) ?? 0;
                if (openConditions > 0)
                    return WorkflowResult.Fail($"There are {openConditions} open condition(s) of approval. Close or waive all conditions before approving.");
            }

            return WorkflowResult.Ok();
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        public async Task ApplyTransition(
            RegulatoryApplication app,
            RIApplicationStatus newStatus,
            string performedBy,
            string? reason = null,
            string? performedByUserId = null)
        {
            var oldStatus = app.Status;
            app.Status    = newStatus;

            // Status-specific side effects
            switch (newStatus)
            {
                case RIApplicationStatus.Submitted:
                    app.SubmissionDate ??= SiteTime.Today;
                    break;

                case RIApplicationStatus.UnderReview:
                    // Set Day 0 only when entering from Submitted for the first time
                    if (oldStatus == RIApplicationStatus.Submitted)
                        app.Day0Date ??= SiteTime.Today;
                    // Reinstatement from Suspended re-opens the record for review.
                    app.IsLocked = false;
                    break;

                case RIApplicationStatus.Approved:
                {
                    app.ApprovalDate       ??= SiteTime.Today;
                    app.ActualDecisionDate ??= SiteTime.Today;
                    app.IsLocked           = true;
                    { var s = await _context.SystemSettings.FindAsync(1) ?? new SystemSettings(); app.RetainUntil = app.ApprovalDate.Value.AddYears(s.RIRetentionYears); }
                    app.RenewalCycle++;

                    var docs = await _context.RIDocuments
                        .Where(d => d.RegulatoryApplicationId == app.Id && d.ApprovalCycle == null)
                        .ToListAsync();
                    foreach (var d in docs) d.ApprovalCycle = app.RenewalCycle;

                    // Resolve display names for navigation/foreign-key fields so the
                    // snapshot is self-contained and human-readable.
                    var jurisdictionName = await _context.RIJurisdictions
                        .Where(j => j.Id == app.JurisdictionId)
                        .Select(j => j.Name)
                        .FirstOrDefaultAsync();
                    var applicationTypeName = await _context.RIApplicationTypes
                        .Where(t => t.Id == app.ApplicationTypeId)
                        .Select(t => t.Name)
                        .FirstOrDefaultAsync();

                    _context.RIApprovalSnapshots.Add(new RIApprovalSnapshot
                    {
                        RegulatoryApplicationId = app.Id,
                        RenewalCycle  = app.RenewalCycle,
                        TakenAt       = DateTime.UtcNow,
                        TakenByUserId = performedByUserId ?? performedBy,
                        SnapshotJson  = JsonSerializer.Serialize(new
                        {
                            // ── Product & Identification ──
                            app.RINumber,
                            app.ProductName,
                            app.ActiveSubstance,
                            app.Indication,
                            app.DosageForm,
                            app.Strength,
                            Jurisdiction    = jurisdictionName,
                            ApplicationType = applicationTypeName,
                            // ── Authority ──
                            app.AuthorityReferenceNumber,
                            app.SubmissionMethod,
                            // ── Priority / Expedited Flags (omitted when not set, matching the live view) ──
                            IsOrphanDesignation   = app.IsOrphanDesignation   ? "Yes" : null,
                            IsPriorityReview      = app.IsPriorityReview      ? "Yes" : null,
                            IsBreakthroughTherapy = app.IsBreakthroughTherapy ? "Yes" : null,
                            IsPRIME               = app.IsPRIME               ? "Yes" : null,
                            IsConditionalApproval = app.IsConditionalApproval ? "Yes" : null,
                            // ── Key Dates ──
                            StrategyStartDate  = app.StrategyStartDate?.ToString("yyyy-MM-dd"),
                            PreSubMeetingDate  = app.PreSubMeetingDate?.ToString("yyyy-MM-dd"),
                            DossierLockDate    = app.DossierLockDate?.ToString("yyyy-MM-dd"),
                            SubmissionDate     = app.SubmissionDate?.ToString("yyyy-MM-dd"),
                            AcknowledgementDate= app.AcknowledgementDate?.ToString("yyyy-MM-dd"),
                            Day0Date           = app.Day0Date?.ToString("yyyy-MM-dd"),
                            TargetDecisionDate = app.TargetDecisionDate?.ToString("yyyy-MM-dd"),
                            ActualDecisionDate = app.ActualDecisionDate?.ToString("yyyy-MM-dd"),
                            ApprovalDate       = app.ApprovalDate?.ToString("yyyy-MM-dd"),
                            ExpiryDate         = app.ExpiryDate?.ToString("yyyy-MM-dd"),
                            NextRenewalDue     = app.NextRenewalDue?.ToString("yyyy-MM-dd"),
                            AppealDeadline     = app.AppealDeadline?.ToString("yyyy-MM-dd"),
                            // ── Fees ──
                            ApplicationFee   = app.ApplicationFee?.ToString("0.00"),
                            FeeCurrency      = app.ApplicationFee.HasValue ? app.FeeCurrency : null,
                            FeePaidDate      = app.FeePaidDate?.ToString("yyyy-MM-dd"),
                            FeeWaiverApplied = app.FeeWaiverApplied ? "Yes" : null,
                            app.FeeWaiverReason,
                            // ── Contacts ──
                            app.InternalRALead,
                            app.ExternalConsultant,
                            app.AuthorityRapporteur,
                            app.AuthorityContactEmail,
                            // ── Notes ──
                            app.StrategyNotes,
                            app.RejectionReason,
                            app.WithdrawalReason,
                            // ── Related records ──
                            Conditions = app.Conditions
                                .Select(c => new { c.ConditionDescription, Status = c.Status.ToString() })
                                .ToList(),
                            Documents = docs
                                .Select(d => new { d.FileName, d.DocumentType, d.FileHash, d.FilePath })
                                .ToList()
                        })
                    });
                    break;
                }

                case RIApplicationStatus.ConditionsActive:
                    app.ApprovalDate       ??= SiteTime.Today;
                    app.ActualDecisionDate ??= SiteTime.Today;
                    app.IsLocked           = false;   // Conditions still need managing
                    break;

                case RIApplicationStatus.Rejected:
                    app.RejectionReason    = reason;
                    app.ActualDecisionDate ??= SiteTime.Today;
                    app.IsLocked           = false;
                    { var s = await _context.SystemSettings.FindAsync(1) ?? new SystemSettings(); app.RetainUntil = app.ActualDecisionDate.Value.AddYears(s.RIRetentionYears); }
                    break;

                case RIApplicationStatus.Withdrawn:
                    app.WithdrawalReason = reason;
                    app.WithdrawnOn      = SiteTime.Today;
                    app.IsLocked         = false;
                    { var s = await _context.SystemSettings.FindAsync(1) ?? new SystemSettings(); app.RetainUntil = app.WithdrawnOn.Value.AddYears(s.RIRetentionYears); }
                    break;

                case RIApplicationStatus.Suspended:
                    app.SuspensionReason = reason;
                    app.SuspendedOn      = SiteTime.Today;
                    app.IsLocked         = true;
                    { var s = await _context.SystemSettings.FindAsync(1) ?? new SystemSettings(); app.RetainUntil = app.SuspendedOn.Value.AddYears(s.RIRetentionYears); }
                    break;

                case RIApplicationStatus.VariationRequired:
                    app.IsLocked = false;    // Unlocked for variation management
                    break;

                case RIApplicationStatus.RenewalDue:
                    app.IsLocked = false;    // Unlocked for renewal submission
                    break;
            }

            app.ModifiedBy = performedBy;
            app.ModifiedAt = DateTime.UtcNow;

            _context.RIAuditLogs.Add(new RIAuditLog
            {
                RegulatoryApplicationId = app.Id,
                Action       = "StatusChanged",
                FieldChanged = "Status",
                OldValue     = oldStatus.ToString(),
                NewValue     = newStatus.ToString(),
                UserId       = performedBy,
                Notes        = reason
            });

            await _context.SaveChangesAsync();
        }
    }
}
