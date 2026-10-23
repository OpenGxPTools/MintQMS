namespace OpenQMS.Models
{
    // Gap 13: Immutable audit trail for all data retention and disposal actions.
    // Protected by BEFORE UPDATE/DELETE triggers (same pattern as Gap 8 audit logs).
    public class DisposalAuditLog
    {
        public int      Id              { get; set; }
        public string   Module          { get; set; } = string.Empty;  // e.g. "CAPA", "AppDocument"
        public int      RecordId        { get; set; }
        public string   RecordReference { get; set; } = string.Empty;  // human-readable ID
        public string   Action          { get; set; } = string.Empty;  // DisposalAction.ToString()
        public string   ActionBy        { get; set; } = string.Empty;
        public DateTime ActionOn        { get; set; }
        public string?  Notes           { get; set; }
    }
}
