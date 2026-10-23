namespace OpenQMS.Models
{
    public class BackupStatusEntry
    {
        public string Status { get; set; } = string.Empty;   // "success" | "failure"
        public DateTime TimestampUtc { get; set; }
        public string? FileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string? UploadsFileName { get; set; }                  // uploaded-files archive (.tar.gz)
        public long UploadsFileSizeBytes { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
