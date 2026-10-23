using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    public class PVRegulatoryService
    {
        private readonly ApplicationDbContext _context;

        public PVRegulatoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public enum DeadlineStatus
        {
            NotApplicable,
            OnTrack,
            Warning,   // ≤5 days remaining
            Critical,  // ≤2 days remaining
            Overdue
        }

        public DeadlineStatus GetDeadlineStatus(AdverseEvent ae)
        {
            if (!ae.IsSerious || !ae.TGADeadlineDate.HasValue || ae.RegulatoryReport?.IsReportableToTGA == false)
                return DeadlineStatus.NotApplicable;

            // Already submitted — no longer at risk
            if (ae.RegulatoryReport?.TGASubmissionDate.HasValue == true)
                return DeadlineStatus.OnTrack;

            var daysLeft = (ae.TGADeadlineDate.Value.Date - SiteTime.Today).Days;

            if (daysLeft < 0)  return DeadlineStatus.Overdue;
            if (daysLeft <= 2) return DeadlineStatus.Critical;
            if (daysLeft <= 5) return DeadlineStatus.Warning;
            return DeadlineStatus.OnTrack;
        }

        public IQueryable<AdverseEvent> GetOverdueCases() =>
            _context.AdverseEvent
                .Include(a => a.RegulatoryReport)
                .Where(a =>
                    a.IsSerious &&
                    a.RegulatoryReport != null &&
                    a.RegulatoryReport.IsReportableToTGA == true &&
                    a.RegulatoryReport.TGASubmissionDate == null &&
                    a.TGADeadlineDate.HasValue &&
                    a.TGADeadlineDate.Value.Date < SiteTime.Today &&
                    a.Status != AdverseEvent.AEStatus.Voided);

        public IQueryable<AdverseEvent> GetApproachingDeadlineCases(int warningDays = 5)
        {
            var cutoff = SiteTime.Today.AddDays(warningDays);
            return _context.AdverseEvent
                .Include(a => a.RegulatoryReport)
                .Where(a =>
                    a.IsSerious &&
                    a.RegulatoryReport != null &&
                    a.RegulatoryReport.IsReportableToTGA == true &&
                    a.RegulatoryReport.TGASubmissionDate == null &&
                    a.TGADeadlineDate.HasValue &&
                    a.TGADeadlineDate.Value.Date >= SiteTime.Today &&
                    a.TGADeadlineDate.Value.Date <= cutoff &&
                    a.Status != AdverseEvent.AEStatus.Voided);
        }

        public async Task LogTGASubmission(
            int aeId,
            DateTime submittedDate,
            string? adrsRef,
            string? method,
            string userId)
        {
            var regReport = await _context.AERegulatoryReport
                .FirstOrDefaultAsync(r => r.AdverseEventId == aeId);

            if (regReport == null) return;

            regReport.TGASubmissionDate = submittedDate;

            if (!string.IsNullOrWhiteSpace(adrsRef))
                regReport.TGAADRSReferenceNumber = adrsRef;

            if (Enum.TryParse<AERegulatoryReport.TGASubmissionMethod>(method, out var parsedMethod))
                regReport.SubmissionMethod = parsedMethod;

            regReport.Status = AERegulatoryReport.AERegulatoryStatus.SubmittedToTGA;

            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = aeId,
                Action = "TGASubmission",
                NewValue = $"Submitted on {submittedDate:dd MMM yyyy}" +
                           (string.IsNullOrWhiteSpace(adrsRef) ? "" : $", ADRS Ref: {adrsRef}"),
                PerformedBy = userId,
                PerformedOn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
    }
}
