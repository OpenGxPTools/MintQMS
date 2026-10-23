using System.Security.Cryptography;

namespace OpenQMS.Services
{
    // Gap 10: computes a SHA-256 fingerprint for uploaded file bytes.
    // Store the returned hex string alongside the file; re-compute on demand to
    // verify the file has not been modified since upload.
    public static class FileHashHelper
    {
        public static string ComputeSha256(byte[] data)
        {
            var hash = SHA256.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
