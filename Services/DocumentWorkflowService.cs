using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.Navigation;

namespace OpenQMS.Services;

public class DocumentWorkflowService
{
    private readonly ApplicationDbContext _context;

    public DocumentWorkflowService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ApplyEffectiveTransition(AppDocument document, string? performedBy, string? commentary)
    {
        var settings = await _context.SystemSettings.FindAsync(1) ?? new SystemSettings();
        document.Status          = AppDocument.DocumentStatus.Effective;
        document.IssuedOn        = DateTime.UtcNow;
        document.IssuedBy        = performedBy;
        document.IssueCommentary = commentary;
        if (document.PeriodicReviewRequired && !document.ReviewDueDate.HasValue)
            document.ReviewDueDate = DateTime.UtcNow.AddMonths(settings.DocumentReviewPeriodMonths);
        _context.AppDocument.Update(document);
    }

    public async Task CheckAndAutoEffective(int trainingId, string? performedBy)
    {
        var dtr = await _context.DocumentTrainingRequirement
            .Where(r => r.TrainingId == trainingId)
            .FirstOrDefaultAsync();
        if (dtr == null) return;

        var allLinked = await _context.DocumentTrainingRequirement
            .Where(r => r.DocumentId == dtr.DocumentId)
            .Include(r => r.Training)
            .ToListAsync();

        if (allLinked.All(r => r.Training.Status == Training.TrainingStatus.Completed))
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(d => d.Id == dtr.DocumentId);
            if (document?.Status == AppDocument.DocumentStatus.Approved)
            {
                await ApplyEffectiveTransition(document, performedBy, null);
                _context.DocumentAuditLog.Add(new DocumentAuditLog
                {
                    AppDocumentId = document.Id,
                    Action        = "Document Issued",
                    FromStatus    = "Approved",
                    ToStatus      = "Effective",
                    PerformedBy   = performedBy ?? "System",
                    PerformedOn   = DateTime.UtcNow,
                    Notes         = "Automatically issued: all training completed."
                });
            }
        }
    }
}
