using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Release
{
    public class ReleaseTestResult
    {
        public int Id { get; set; }
        public int ReleaseRecordId { get; set; }
        public int DisplayOrder { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Test Parameter")]
        public string TestParameter { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Test Method")]
        public string? TestMethod { get; set; }

        [Required, MaxLength(300)]
        [Display(Name = "Acceptance Criterion")]
        public string AcceptanceCriterion { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Result { get; set; }

        public TestResultStatus Status { get; set; } = TestResultStatus.Pending;

        [MaxLength(500)]
        public string? Comment { get; set; }

        [MaxLength(200)]
        public string? EnteredBy { get; set; }

        public DateTime? EnteredDate { get; set; }

        public bool IsLocked { get; set; } = false;

        public ReleaseRecord? ReleaseRecord { get; set; }
    }
}
