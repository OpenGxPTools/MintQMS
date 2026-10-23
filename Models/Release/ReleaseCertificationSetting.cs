namespace OpenQMS.Models.Release
{
    public class ReleaseCertificationSetting
    {
        public int Id { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public string ProductGroup { get; set; } = "";
        public string ProductGroupLabel { get; set; } = "";
        public string Statement { get; set; } = "";
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedDate { get; set; }
    }
}
