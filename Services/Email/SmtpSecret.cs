using Microsoft.AspNetCore.DataProtection;

namespace OpenQMS.Services.Email
{
    /// <summary>
    /// Protects the SMTP password at rest with ASP.NET Data Protection (keys in DataProtectionKeysPath). A stored value
    /// starts with "dp1:" when protected; an older plain value is still read, and is protected at start-up. If the keys
    /// are lost (e.g. a database restored without its keys folder) the password cannot be read: re-enter it in System
    /// Settings. Never logged, shown or exported.
    /// </summary>
    public class SmtpSecret
    {
        private const string Prefix = "dp1:";
        private readonly IDataProtector _protector;

        public SmtpSecret(IDataProtectionProvider provider) =>
            _protector = provider.CreateProtector("MintQMS.SystemSettings.SmtpPassword.v1");

        public static bool IsProtected(string? stored) => stored?.StartsWith(Prefix, StringComparison.Ordinal) == true;

        public string Protect(string plain) => Prefix + _protector.Protect(plain);

        /// <summary>The password, or null when none is set. Throws when a protected value cannot be read.</summary>
        public string? Reveal(string? stored)
        {
            if (string.IsNullOrEmpty(stored))
                return null;
            if (!IsProtected(stored))
                return stored;
            try
            {
                return _protector.Unprotect(stored[Prefix.Length..]);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The SMTP password cannot be read (data-protection keys changed or lost). Re-enter it in System Settings.", ex);
            }
        }
    }
}
