namespace OpenQMS.Models
{
    // Gap 6: stores previous password hashes per user for reuse prevention.
    public class PasswordHistory
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
