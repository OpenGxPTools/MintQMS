namespace OpenQMS.Models.Navigation
{
    public class TrainingAttachment
    {
        public int Id { get; set; }
        public int UserTrainingId { get; set; }
        public UserTraining UserTraining { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string? FileHash { get; set; }   // SHA-256 of Content, recorded at upload
        public string ContentType { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedOn { get; set; }
    }
}
