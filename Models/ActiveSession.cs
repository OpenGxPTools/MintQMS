namespace OpenQMS.Models
{
    // Gap 11: tracks one row per active authenticated session.
    // Used to detect concurrent logins and log them to LoginAuditLog.
    public class ActiveSession
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string SessionToken { get; set; } = string.Empty;  // GUID set in cookie
        public DateTime LoggedInAt { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
    }
}
