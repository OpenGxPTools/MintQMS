using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Release
{
    public class ReleaseCrossReference
    {
        public int Id { get; set; }
        public int ReleaseRecordId { get; set; }

        [Required, MaxLength(50)]
        public string ModuleType { get; set; } = string.Empty;

        public int? LinkedRecordId { get; set; }

        [MaxLength(50)]
        public string? LinkedRecordNumber { get; set; }

        [MaxLength(500)]
        public string? LinkedRecordTitle { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public ReleaseRecord? ReleaseRecord { get; set; }
    }
}
