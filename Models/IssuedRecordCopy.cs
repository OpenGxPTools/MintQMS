using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    /// <summary>
    /// A complete, fixed copy of a document the system ISSUES (a release certificate, a trainee's
    /// training certificate, a training attendance list), saved at the moment it is issued and shown
    /// from then on exactly as saved. Nothing about it changes afterwards: not a later language or
    /// settings change (organisation name, logo), not a renamed user, not a software update.
    /// A copy is never edited or deleted; when its record is reopened it is marked superseded and a
    /// new copy is saved at the next issue.
    /// </summary>
    public class IssuedRecordCopy
    {
        public int Id { get; set; }

        /// <summary>What was issued — one of <see cref="IssuedRecordKinds"/>.</summary>
        [Required, MaxLength(40)]
        public string Kind { get; set; } = string.Empty;

        /// <summary>Id of the source row: ReleaseCertificate.Id, UserTraining.Id or Training.Id.</summary>
        public int SourceId { get; set; }

        /// <summary>Language the copy was issued in (site language at issue), e.g. "en", "es", "pt-BR".</summary>
        [Required, MaxLength(10)]
        public string Language { get; set; } = "en";

        /// <summary>The issued document body (self-contained HTML; the logo is embedded).</summary>
        [Required, Column(TypeName = "TEXT")]
        public string Html { get; set; } = string.Empty;

        /// <summary>SHA-256 (hex) of <see cref="Html"/> as UTF-8, checked every time the copy is shown.</summary>
        [Required, MaxLength(64)]
        public string Sha256 { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; }

        [MaxLength(256)]
        public string? CreatedBy { get; set; }

        /// <summary>"Issued", or "FrozenOnUpgrade" for documents issued before copies were kept.</summary>
        [Required, MaxLength(20)]
        public string Origin { get; set; } = IssuedRecordOrigins.Issued;

        public DateTime? SupersededOn { get; set; }

        [MaxLength(500)]
        public string? SupersededReason { get; set; }
    }

    public static class IssuedRecordKinds
    {
        public const string ReleaseCertificate  = "ReleaseCertificate";
        public const string TrainingCertificate = "TrainingCertificate";
        public const string AttendanceList      = "AttendanceList";
    }

    public static class IssuedRecordOrigins
    {
        public const string Issued          = "Issued";
        public const string FrozenOnUpgrade = "FrozenOnUpgrade";
    }
}
