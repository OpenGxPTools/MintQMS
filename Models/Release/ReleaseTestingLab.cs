using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Release
{
    public class ReleaseTestingLab
    {
        public int Id { get; set; }
        public int ReleaseRecordId { get; set; }

        [Required, MaxLength(500)]
        [Display(Name = "Testing Laboratory")]
        public string LaboratoryName { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Testing Site GMP Licence / Clearance No.")]
        public string? GmpLicenceRef { get; set; }

        public virtual ReleaseRecord? ReleaseRecord { get; set; }
    }
}
