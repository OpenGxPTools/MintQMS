using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    /// <summary>
    /// Validates business rules before PV workflow status transitions.
    /// </summary>
    public class PVWorkflowService
    {
        private readonly ApplicationDbContext _context;

        public PVWorkflowService(ApplicationDbContext context)
        {
            _context = context;
        }

        public class TransitionResult
        {
            public bool Success { get; init; }
            public string? Error { get; init; }
            public static TransitionResult Ok() => new() { Success = true };
            public static TransitionResult Fail(string error) => new() { Success = false, Error = error };
        }

        /// <summary>Validates that a transition is allowed given business rules.</summary>
        public async Task<TransitionResult> ValidateTransition(
            AdverseEvent ae,
            AdverseEvent.AEStatus newStatus)
        {
            switch (newStatus)
            {
                case AdverseEvent.AEStatus.UnderMedicalReview:
                    if (ae.Status != AdverseEvent.AEStatus.Draft)
                        return TransitionResult.Fail("Can only submit for review from Draft status.");
                    if (ae.IsSerious && !ae.Day0Date.HasValue)
                        return TransitionResult.Fail("Day 0 Date is required for serious adverse events before submitting for review.");
                    // ICSR minimum 4 elements check
                    var missing = new List<string>();
                    var reporter = await _context.AEReporter.FirstOrDefaultAsync(r => r.AdverseEventId == ae.Id);
                    var patient = await _context.AEPatient.FirstOrDefaultAsync(p => p.AdverseEventId == ae.Id);
                    var product = await _context.AESuspectProduct.FirstOrDefaultAsync(s => s.AdverseEventId == ae.Id);
                    if (reporter == null || string.IsNullOrWhiteSpace(reporter.ReporterName))
                        missing.Add("Identifiable Reporter");
                    if (patient == null || string.IsNullOrWhiteSpace(patient.PatientIdentifier))
                        missing.Add("Identifiable Patient");
                    if (product == null || string.IsNullOrWhiteSpace(product.ProductName))
                        missing.Add("Suspect Medicine / Device");
                    if (string.IsNullOrWhiteSpace(ae.AEDescription))
                        missing.Add("Adverse Event Description");
                    if (missing.Any())
                        return TransitionResult.Fail($"Missing required ICSR elements: {string.Join(", ", missing)}.");
                    break;

                case AdverseEvent.AEStatus.PendingApproval:
                    if (ae.Status != AdverseEvent.AEStatus.UnderMedicalReview)
                        return TransitionResult.Fail("Can only move to Pending Approval from Under Medical Review.");
                    if (string.IsNullOrWhiteSpace(ae.MedicalNarrative))
                        return TransitionResult.Fail("Medical Narrative is required before submitting for approval.");
                    // Check overdue TGA submission
                    var regReport = await _context.AERegulatoryReport.FirstOrDefaultAsync(r => r.AdverseEventId == ae.Id);
                    if (ae.IsSerious && regReport?.IsReportableToTGA == true
                        && !regReport.TGASubmissionDate.HasValue
                        && ae.TGADeadlineDate.HasValue
                        && SiteTime.Today > ae.TGADeadlineDate.Value.Date)
                    {
                        return TransitionResult.Fail(
                            $"TGA submission deadline ({ae.TGADeadlineDate.Value:dd MMM yyyy}) has passed without a recorded submission date. " +
                            "Record the submission date on the Regulatory tab before proceeding.");
                    }
                    break;

                case AdverseEvent.AEStatus.Approved:
                    if (ae.Status != AdverseEvent.AEStatus.PendingApproval)
                        return TransitionResult.Fail("Can only approve from Pending Approval status.");
                    break;

                case AdverseEvent.AEStatus.Closed:
                    if (ae.Status != AdverseEvent.AEStatus.Approved)
                        return TransitionResult.Fail("Can only close an Approved case.");
                    break;

                case AdverseEvent.AEStatus.Draft:
                    // Send back from UnderMedicalReview
                    if (ae.Status != AdverseEvent.AEStatus.UnderMedicalReview &&
                        ae.Status != AdverseEvent.AEStatus.PendingApproval)
                        return TransitionResult.Fail("Can only send back from Under Medical Review or Pending Approval.");
                    break;

                case AdverseEvent.AEStatus.Voided:
                    if (ae.IsLocked)
                        return TransitionResult.Fail("Approved records cannot be voided.");
                    break;
            }

            return TransitionResult.Ok();
        }

        /// <summary>Applies the transition: sets status, locks on Approve/Close, flags PSUR on Close, writes audit log.</summary>
        public async Task ApplyTransition(
            AdverseEvent ae,
            AdverseEvent.AEStatus newStatus,
            string performedBy,
            string? comment = null)
        {
            var oldStatus = ae.Status.ToString();

            ae.Status = newStatus;

            // Approved and Closed are both immutable, locked states. A case stays locked
            // once approved and remains locked through closure; further information is
            // added only via a Follow-up Report.
            if (newStatus == AdverseEvent.AEStatus.Approved ||
                newStatus == AdverseEvent.AEStatus.Closed)
                ae.IsLocked = true;

            // Closing a concluded case flags it for inclusion in the next PSUR
            // (unless it has already been rolled into a periodic report).
            if (newStatus == AdverseEvent.AEStatus.Closed)
            {
                var reg = await _context.AERegulatoryReport
                    .FirstOrDefaultAsync(r => r.AdverseEventId == ae.Id);
                if (reg != null && reg.PSURId == null)
                    reg.IncludeInNextPSUR = true;
            }

            ae.ModifiedBy = performedBy;
            ae.ModifiedOn = DateTime.UtcNow;

            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = ae.Id,
                Action = "StatusTransition",
                OldValue = oldStatus,
                NewValue = newStatus.ToString(),
                Notes = comment,
                PerformedBy = performedBy,
                PerformedOn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        /// <summary>Creates a follow-up AE from an approved/closed case.</summary>
        public async Task<AdverseEvent> CreateFollowUp(AdverseEvent parent, string createdBy)
        {
            // Determine suffix — count existing follow-ups
            var existingFollowUps = await _context.AdverseEvent
                .CountAsync(a => a.ParentAEId == parent.Id);
            var suffix = $"/F{existingFollowUps + 1}";

            var followUp = new AdverseEvent
            {
                AENumber = parent.AENumber + suffix,
                ReportDate = DateTime.UtcNow,
                ReportType = AdverseEvent.AEReportType.FollowUp,
                ReportSource = parent.ReportSource,
                ParentAEId = parent.Id,
                Status = AdverseEvent.AEStatus.Draft,
                IsSerious = parent.IsSerious,
                SeriousnessCriteria = parent.SeriousnessCriteria,
                IsFatalOrLifeThreatening = parent.IsFatalOrLifeThreatening,
                IsReportable = parent.IsReportable,
                IsLocked = false,
                AEDescription = parent.AEDescription,
                MedDRAPT = parent.MedDRAPT,
                MedDRASOC = parent.MedDRASOC,
                Causality = parent.Causality,
                Severity = parent.Severity,
                Expectedness = parent.Expectedness,
                MedicalEvaluator = parent.MedicalEvaluator,
                CreatedBy = createdBy,
                CreatedOn = DateTime.UtcNow
            };

            // Recalculate deadline from today as new Day0
            followUp.Day0Date = SiteTime.Today;
            var days = followUp.IsFatalOrLifeThreatening ? 7 : 15;
            followUp.TGADeadlineDate = followUp.Day0Date.Value.AddDays(days);

            _context.AdverseEvent.Add(followUp);
            await _context.SaveChangesAsync();

            // Copy reporter/patient/product as starting point
            var parentReporter = await _context.AEReporter.FirstOrDefaultAsync(r => r.AdverseEventId == parent.Id);
            var parentPatient = await _context.AEPatient.FirstOrDefaultAsync(p => p.AdverseEventId == parent.Id);
            var parentProduct = await _context.AESuspectProduct.FirstOrDefaultAsync(s => s.AdverseEventId == parent.Id);

            if (parentReporter != null)
                _context.AEReporter.Add(new AEReporter
                {
                    AdverseEventId = followUp.Id,
                    ReporterName = parentReporter.ReporterName,
                    Type = parentReporter.Type,
                    Qualification = parentReporter.Qualification,
                    Institution = parentReporter.Institution,
                    Country = parentReporter.Country
                });

            if (parentPatient != null)
                _context.AEPatient.Add(new AEPatient
                {
                    AdverseEventId = followUp.Id,
                    PatientIdentifier = parentPatient.PatientIdentifier,
                    Sex = parentPatient.Sex,
                    Age = parentPatient.Age,
                    AgeUnit = parentPatient.AgeUnit
                });

            if (parentProduct != null)
                _context.AESuspectProduct.Add(new AESuspectProduct
                {
                    AdverseEventId = followUp.Id,
                    ProductId = parentProduct.ProductId,
                    ProductName = parentProduct.ProductName,
                    ARTGNumber = parentProduct.ARTGNumber
                });

            _context.AERegulatoryReport.Add(new AERegulatoryReport
            {
                AdverseEventId = followUp.Id,
                Status = AERegulatoryReport.AERegulatoryStatus.PendingSubmission
            });

            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = parent.Id,
                Action = "FollowUpCreated",
                NewValue = followUp.AENumber,
                PerformedBy = createdBy,
                PerformedOn = DateTime.UtcNow
            });

            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = followUp.Id,
                Action = "Created",
                NewValue = $"Follow-up of {parent.AENumber}",
                PerformedBy = createdBy,
                PerformedOn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return followUp;
        }
    }
}
