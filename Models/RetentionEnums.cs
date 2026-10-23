namespace OpenQMS.Models
{
    // Gap 13: Reason codes for placing a Retention Extended hold on a record.
    public enum RetentionHoldReason
    {
        Legal               = 1,
        Regulatory          = 2,
        BusinessRequirements = 3
    }

    // Gap 13: Action types recorded in DisposalAuditLog.
    public enum DisposalAction
    {
        RetentionCalculated,       // RetainUntil set when record reached terminal status
        HoldSet,                   // Retention Extended flag applied
        HoldRemoved,               // Retention Extended flag removed (Admin + e-sig)
        DisposalAttemptBlocked,    // Disposal attempted on a held record — blocked
        Disposed,                  // Record soft-deleted as part of approved disposal
        ExportGenerated            // Disposal-eligible report exported
    }
}
