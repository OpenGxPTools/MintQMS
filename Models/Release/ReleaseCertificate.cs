using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models.Release
{
    public class ReleaseCertificate
    {
        public int Id { get; set; }
        public int ReleaseRecordId { get; set; }

        [Required, MaxLength(30)]
        public string CertificateNumber { get; set; } = string.Empty;

        public CertificateType CertificateType { get; set; }

        public CertificateStatus Status { get; set; } = CertificateStatus.Draft;

        [Column(TypeName = "TEXT")]
        public string? ContentSnapshot { get; set; }

        [MaxLength(500)]
        public string? PdfFilePath { get; set; }

        public int Version { get; set; } = 1;

        [MaxLength(500)]
        public string? SupersededReason { get; set; }

        public int? SupersededById { get; set; }

        public DateTime IssuedDate { get; set; }

        [MaxLength(200)]
        public string? IssuedBy { get; set; }

        [MaxLength(300)]
        public string? APFullNameQualification { get; set; }

        public ReleaseRecord? ReleaseRecord { get; set; }
    }
}
