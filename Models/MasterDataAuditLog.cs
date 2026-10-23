namespace OpenQMS.Models
{
    public class MasterDataAuditLog
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;   // e.g. "Product", "Material"
        public int ParentId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime PerformedOn { get; set; }
        public string? Notes { get; set; }
    }
}
