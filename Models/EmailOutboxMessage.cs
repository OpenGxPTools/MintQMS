using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    /// <summary>
    /// An e-mail waiting to be sent, sent, or given up on. Written in the same database save as the event that causes
    /// it (e.g. a task assignment), and sent by the background sender with retries, so the workflow never waits for — or
    /// fails because of — the mail server. Rows are kept as the record of what was sent.
    /// </summary>
    public class EmailOutboxMessage
    {
        public int Id { get; set; }

        [MaxLength(256)]
        public string ToAddress { get; set; } = string.Empty;
        [MaxLength(300)]
        public string Subject { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;

        /// <summary>What caused it, e.g. "TaskAssigned".</summary>
        [MaxLength(40)]
        public string Kind { get; set; } = string.Empty;
        [MaxLength(40)]
        public string? Module { get; set; }
        public int? RecordId { get; set; }

        public DateTime CreatedOn { get; set; }
        /// <summary>Pending, Sent or Failed (given up after the last retry).</summary>
        [MaxLength(10)]
        public string Status { get; set; } = EmailOutboxStatus.Pending;
        public int Attempts { get; set; }
        public DateTime NextAttemptOn { get; set; }
        public DateTime? SentOn { get; set; }
        [MaxLength(1000)]
        public string? LastError { get; set; }
    }

    public static class EmailOutboxStatus
    {
        public const string Pending = "Pending";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
    }
}
