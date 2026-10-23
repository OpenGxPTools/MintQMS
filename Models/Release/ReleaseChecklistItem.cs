using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Release
{
    public class ReleaseChecklistItem
    {
        public int Id { get; set; }
        public int ReleaseRecordId { get; set; }

        [Required, MaxLength(300)]
        public string ItemText { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Category { get; set; }

        public int DisplayOrder { get; set; }

        public ChecklistItemStatus Status { get; set; } = ChecklistItemStatus.NotReviewed;

        [MaxLength(1000)]
        public string? Comment { get; set; }

        [MaxLength(200)]
        public string? ReviewedBy { get; set; }

        public DateTime? ReviewedDate { get; set; }

        public bool IsBlocking { get; set; } = false;

        public bool IsRequired { get; set; } = true;

        public ReleaseRecord? ReleaseRecord { get; set; }
    }
}
