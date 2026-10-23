using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.Release;

namespace OpenQMS.Services
{
    public class ReleaseWorkflowService
    {
        public class WorkflowResult
        {
            public bool    Success { get; init; }
            public string? Error   { get; init; }

            public static WorkflowResult Ok()               => new() { Success = true };
            public static WorkflowResult Fail(string error) => new() { Success = false, Error = error };
        }

        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReleaseWorkflowService> _logger;

        // Valid FSM transitions: (from, to)
        private static readonly HashSet<(ReleaseStatus, ReleaseStatus)> _validTransitions = new()
        {
            (ReleaseStatus.Draft,             ReleaseStatus.UnderReview),
            (ReleaseStatus.Draft,             ReleaseStatus.Cancelled),
            (ReleaseStatus.UnderReview,       ReleaseStatus.OnHold),
            (ReleaseStatus.UnderReview,       ReleaseStatus.PendingAPDecision),
            (ReleaseStatus.UnderReview,       ReleaseStatus.Draft),
            (ReleaseStatus.UnderReview,       ReleaseStatus.Cancelled),      // Finding 4: direct cancel from review
            (ReleaseStatus.OnHold,            ReleaseStatus.UnderReview),
            (ReleaseStatus.OnHold,            ReleaseStatus.Draft),          // Finding 8: lift hold directly to Draft
            (ReleaseStatus.OnHold,            ReleaseStatus.Cancelled),
            (ReleaseStatus.PendingAPDecision, ReleaseStatus.OnHold),
            (ReleaseStatus.PendingAPDecision, ReleaseStatus.Certified),
            (ReleaseStatus.PendingAPDecision, ReleaseStatus.Rejected),
            (ReleaseStatus.PendingAPDecision, ReleaseStatus.UnderReview),
            (ReleaseStatus.Certified,         ReleaseStatus.Released),
            (ReleaseStatus.Certified,         ReleaseStatus.Closed),
            (ReleaseStatus.Released,          ReleaseStatus.Closed),
            (ReleaseStatus.Rejected,          ReleaseStatus.Closed),
            (ReleaseStatus.Rejected,          ReleaseStatus.UnderReview),   // Finding 5: AP can override rejection
        };

        public ReleaseWorkflowService(
            ApplicationDbContext context,
            ILogger<ReleaseWorkflowService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public WorkflowResult ValidateTransition(ReleaseRecord record, ReleaseStatus targetStatus, string? reason = null)
        {
            if (!_validTransitions.Contains((record.Status, targetStatus)))
                return WorkflowResult.Fail($"Transition from {record.Status} to {targetStatus} is not permitted.");

            // Blocking rule: cannot submit to AP if any blocking checklist item is Outstanding or Not Applicable.
            // IsBlocking is set at record creation time and already accounts for release type —
            // items that are non-blocking for Quality Oversight are created with IsBlocking=false.
            if (targetStatus == ReleaseStatus.PendingAPDecision && record.ChecklistItems != null)
            {
                bool hasBlocker = record.ChecklistItems.Any(i =>
                    i.IsBlocking && (
                        i.Status == ChecklistItemStatus.Outstanding ||
                        i.Status == ChecklistItemStatus.NotApplicable
                    ));
                if (hasBlocker)
                    return WorkflowResult.Fail("Cannot submit to AP: one or more blocking checklist items are unresolved. Resolve all blocking items before submitting.");
            }

            // Key fields must be populated before submitting for review
            if (targetStatus == ReleaseStatus.UnderReview)
            {
                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(record.BatchNumber))       missing.Add("Batch Number");
                if (record.ManufacturingDate == null)                    missing.Add("Manufacturing Date");
                if (string.IsNullOrWhiteSpace(record.ManufacturingSite)) missing.Add("Manufacturing Site");
                if (missing.Any())
                    return WorkflowResult.Fail($"Cannot submit for review: required fields are missing — {string.Join(", ", missing)}.");
            }

            // Blocking rule: cannot submit to AP if any analytical result is still Pending
            if (targetStatus == ReleaseStatus.PendingAPDecision && record.TestResults != null)
            {
                bool hasPendingResult = record.TestResults.Any(t =>
                    t.Status == TestResultStatus.Pending);
                if (hasPendingResult)
                    return WorkflowResult.Fail("Cannot submit to AP: one or more analytical test results are still pending. Enter a result (Pass, Fail, or N/A) for all listed tests before submitting.");
            }

            // Key fields must be populated before AP submission
            if (targetStatus == ReleaseStatus.PendingAPDecision)
            {
                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(record.BatchNumber))            missing.Add("Batch Number");
                if (record.ManufacturingDate == null)                         missing.Add("Manufacturing Date");
                if (record.ExpiryDate == null && record.RetestDate == null)   missing.Add("Expiry Date or Retest Date");
                if (string.IsNullOrWhiteSpace(record.ManufacturingSite))      missing.Add("Manufacturing Site");
                if (string.IsNullOrWhiteSpace(record.ReleasingCompany))       missing.Add("Releasing Company");
                if (missing.Any())
                    return WorkflowResult.Fail($"Cannot submit to AP: required fields are missing — {string.Join(", ", missing)}.");
            }

            // Reject / Certified require a reason or password — validated separately in controller

            if (targetStatus == ReleaseStatus.OnHold && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A hold reason is required.");

            if (targetStatus == ReleaseStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A cancellation reason is required.");

            if (targetStatus == ReleaseStatus.Rejected && string.IsNullOrWhiteSpace(reason))
                return WorkflowResult.Fail("A rejection reason is required.");

            return WorkflowResult.Ok();
        }

        public async Task<WorkflowResult> TransitionAsync(
            int releaseRecordId,
            ReleaseStatus targetStatus,
            string performedBy,
            string? comments = null,
            string? apFullName = null)
        {
            var record = await _context.ReleaseRecords
                .Include(r => r.ChecklistItems)
                .Include(r => r.TestResults)
                .FirstOrDefaultAsync(r => r.Id == releaseRecordId);

            if (record == null)
                return WorkflowResult.Fail("Release record not found.");

            var validation = ValidateTransition(record, targetStatus, comments);
            if (!validation.Success)
                return validation;

            var fromStatusEnum = record.Status;
            var fromStatus = record.Status.ToString();
            var wasOnHold = record.Status == ReleaseStatus.OnHold;
            var wasRejected = record.Status == ReleaseStatus.Rejected;

            // SQLite trigger blocks UPDATE when OLD.IsLocked=1 AND NEW.IsLocked=1.
            // Use an explicit transaction: if the record is currently locked, unlock it first
            // (NEW.IsLocked=0 bypasses the trigger), then apply the full transition.
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (record.IsLocked)
                {
                    record.IsLocked = false;
                    await _context.SaveChangesAsync();
                }

                record.Status = targetStatus;
                record.LastModifiedDate = DateTime.UtcNow;
                record.LastModifiedBy = performedBy;

                // Status-specific side effects
                switch (targetStatus)
                {
                    case ReleaseStatus.OnHold:
                        record.HoldReason = comments;
                        record.HoldDate = DateTime.UtcNow;
                        record.HoldBy = performedBy;
                        break;

                    case ReleaseStatus.UnderReview when wasOnHold:
                        record.HoldReason = null;
                        record.HoldDate = null;
                        record.HoldBy = null;
                        break;

                    case ReleaseStatus.UnderReview when wasRejected:
                        record.RejectionReason = null;
                        record.RejectionDate = null;
                        record.RejectedBy = null;
                        break;

                    case ReleaseStatus.Draft when wasOnHold:
                        record.HoldReason = null;
                        record.HoldDate = null;
                        record.HoldBy = null;
                        break;

                    case ReleaseStatus.Certified:
                        record.CertificationDate = DateTime.UtcNow;
                        record.CertifiedBy = performedBy;
                        if (!string.IsNullOrWhiteSpace(apFullName))
                            record.APFullNameQualification = apFullName;
                        record.IsLocked = true;
                        foreach (var tr in await _context.ReleaseTestResults
                            .Where(t => t.ReleaseRecordId == releaseRecordId).ToListAsync())
                            tr.IsLocked = true;
                        break;

                    case ReleaseStatus.Rejected:
                        // Do NOT lock on rejection — allow documents/notes until Closed
                        record.RejectionReason = comments;
                        record.RejectionDate = DateTime.UtcNow;
                        record.RejectedBy = performedBy;
                        break;

                    case ReleaseStatus.Released:
                    case ReleaseStatus.Closed:
                    case ReleaseStatus.Cancelled:
                        record.IsLocked = true;
                        break;
                }

                _context.ReleaseAuditEntries.Add(new ReleaseAuditEntry
                {
                    ReleaseRecordId = releaseRecordId,
                    Action = GetActionLabel(fromStatusEnum, targetStatus),
                    FromStatus = fromStatus,
                    ToStatus = targetStatus.ToString(),
                    Details = comments,
                    PerformedBy = performedBy,
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            _logger.LogInformation("[Release] {RecordId} transitioned {From} → {To} by {User}",
                releaseRecordId, fromStatus, targetStatus, performedBy);

            return WorkflowResult.Ok();
        }

        public bool HasOutstandingChecklistItems(ReleaseRecord record)
        {
            return record.ChecklistItems?.Any(i => i.Status == ChecklistItemStatus.Outstanding) ?? false;
        }

        private static string GetActionLabel(ReleaseStatus from, ReleaseStatus to) => (from, to) switch
        {
            (ReleaseStatus.OnHold,    ReleaseStatus.UnderReview) => "Hold Lifted",
            (ReleaseStatus.OnHold,    ReleaseStatus.Draft)       => "Hold Lifted — Returned to Draft",
            (ReleaseStatus.Rejected,  ReleaseStatus.UnderReview) => "Rejection Overridden by AP",
            (_,                       ReleaseStatus.UnderReview) => "Submitted for Review",
            (_,                       ReleaseStatus.OnHold)            => "Placed On Hold",
            (_,                       ReleaseStatus.PendingAPDecision) => "Submitted to AP",
            (_,                       ReleaseStatus.Draft)             => "Returned to Draft",
            (_,                       ReleaseStatus.Certified)         => "Certified by AP",
            (_,                       ReleaseStatus.Released)          => "Transfer to Saleable Stock Confirmed",
            (_,                       ReleaseStatus.Rejected)          => "Rejected by AP",
            (_,                       ReleaseStatus.Closed)            => "Closed",
            (_,                       ReleaseStatus.Cancelled)         => "Cancelled",
            _                                                           => to.ToString()
        };
    }
}
