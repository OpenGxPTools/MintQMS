using OpenQMS.Models.Release;
using OpenQMS.Services;

namespace OpenQMS.ViewModels.Release
{
    /// <summary>Everything a release certificate shows, fixed at the moment of issue.</summary>
    public class CertificateDocumentModel
    {
        public CertificateSnapshot Snapshot { get; init; } = new();
        public CertificateType? CertificateType { get; init; }
        public string? APFullNameQualification { get; init; }
        public IssuedOrgHeader Org { get; init; } = new();
    }
}
