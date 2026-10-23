namespace OpenQMS.Services
{
    // EU Annex 11 §4.2 / 21 CFR Part 11 §11.10(a): verify an uploaded file on retrieval against the
    // SHA-256 recorded at upload (FileHashHelper). A mismatch means the stored file changed after
    // upload — the file is not served and the failure is written to the parent record's audit trail.
    public static class FileIntegrity
    {
        public const string AuditAction = "Integrity check failed";

        /// <summary>
        /// True when the file matches its recorded hash, or when no hash was recorded
        /// (files uploaded before hashing was introduced cannot be verified).
        /// </summary>
        public static async Task<bool> MatchesAsync(string path, string? storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return true;
            return Matches(await File.ReadAllBytesAsync(path), storedHash);
        }

        /// <summary>Same check for files held in the database.</summary>
        public static bool Matches(byte[] content, string? storedHash) =>
            string.IsNullOrEmpty(storedHash)
            || string.Equals(FileHashHelper.ComputeSha256(content), storedHash, StringComparison.OrdinalIgnoreCase);

        public static string AuditNote(string fileName) =>
            $"{fileName}: stored file does not match the SHA-256 recorded at upload — not served.";

        public static string FailureMessage(string fileName) =>
            $"WARNING: The file '{fileName}' failed its integrity check — its content no longer matches the " +
            "fingerprint recorded at upload, so it has not been opened. This has been logged. Contact your system administrator.";
    }
}
