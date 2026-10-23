namespace OpenQMS.Models
{
    public class RiskAuditLog
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime PerformedOn { get; set; }
        public string? Notes { get; set; }
    }
}
