namespace OpenQMS.Models
{
    public class SettingsAuditLog
    {
        public int Id { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime ChangedOn { get; set; }
    }
}
