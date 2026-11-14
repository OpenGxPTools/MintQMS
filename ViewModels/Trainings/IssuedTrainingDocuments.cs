using OpenQMS.Models;
using OpenQMS.Models.Navigation;
using OpenQMS.Services;

namespace OpenQMS.ViewModels.Trainings
{
    /// <summary>A trainee's training certificate, as fixed when that trainee's training was completed.</summary>
    public class TrainingCertificateDocumentModel
    {
        public Training Training { get; init; } = null!;
        public UserTraining Row { get; init; } = null!;
        public AppUser? Trainee { get; init; }
        public AppDocument? PolicyDocument { get; init; }
        /// <summary>When the certificate was issued (UTC) — the moment the copy was made.</summary>
        public DateTime IssuedOn { get; init; }
        public IssuedOrgHeader Org { get; init; } = new();
    }

    /// <summary>A training's attendance list (completion record): fixed when the training is completed,
    /// shown live (marked as not yet final) while it is still open.</summary>
    public class AttendanceListDocumentModel
    {
        public Training Training { get; init; } = null!;
        public List<UserTraining> Rows { get; init; } = new();
        public AppUser? CompletedByUser { get; init; }
        public AppDocument? PolicyDocument { get; init; }
        public DateTime IssuedOn { get; init; }
        public IssuedOrgHeader Org { get; init; } = new();
        public bool IsLive { get; set; }
    }
}
