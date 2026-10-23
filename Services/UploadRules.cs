namespace OpenQMS
{
    /// <summary>
    /// Single source of the file types and size limit accepted for record attachments and
    /// documents, shared by every upload action and the upload forms.
    /// </summary>
    public static class UploadRules
    {
        public static readonly string[] AllowedExtensions =
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".odt", ".ods", ".odp",
            ".ppt", ".pptx", ".csv", ".zip", ".jpg", ".jpeg", ".png"
        };

        /// <summary>Maximum size of a file-system stored attachment/document (25 MB).</summary>
        public const long MaxBytes = 25L * 1024 * 1024;

        /// <summary>Value for an &lt;input type="file" accept="…"&gt; attribute.</summary>
        public static readonly string Accept = string.Join(",", AllowedExtensions);

        /// <summary>Human-readable list for form hints and error messages.</summary>
        public const string AcceptedLabel = "PDF, Word, Excel, PowerPoint, OpenDocument, CSV, ZIP, JPG, PNG";

        public static bool IsAllowed(string? fileName) =>
            AllowedExtensions.Contains(Path.GetExtension(fileName ?? "").ToLowerInvariant());
    }
}
