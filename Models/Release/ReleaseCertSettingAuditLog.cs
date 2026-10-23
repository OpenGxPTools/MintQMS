namespace OpenQMS.Models.Release
{
    /// <summary>
    /// Change history for release certification statement settings.
    /// One row per field change (currently the certification Statement), giving a
    /// GMP audit trail for the wording stamped onto release certificates.
    /// </summary>
    public class ReleaseCertSettingAuditLog
    {
        public int Id { get; set; }
        public int ReleaseCertificationSettingId { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public string ProductGroupLabel { get; set; } = string.Empty;
        public string FieldName { get; set; } = "Statement";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime ChangedOn { get; set; }
    }
}
