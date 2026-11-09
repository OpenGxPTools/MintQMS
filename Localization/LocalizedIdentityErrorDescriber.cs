using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Sign-in and password-rule messages in the user's language. When the interface language is
    /// English the framework's own message is returned untouched, so English cannot change; in any
    /// other language the English template below is the translation key (no translation → English).
    /// These messages are shown to the user only; none is stored.
    /// </summary>
    public class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
    {
        private readonly IStringLocalizer<SharedResource> _l;
        public LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> l) => _l = l;

        internal static bool English => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

        private IdentityError T(IdentityError e, string template, params object[] args)
        {
            if (English) return e;
            var s = _l[template, args];
            if (!s.ResourceNotFound) e.Description = s.Value;
            return e;
        }

        public override IdentityError DefaultError() => T(base.DefaultError(), "An unknown failure has occurred.");
        public override IdentityError ConcurrencyFailure() => T(base.ConcurrencyFailure(), "Optimistic concurrency failure, object has been modified.");
        public override IdentityError PasswordMismatch() => T(base.PasswordMismatch(), "Incorrect password.");
        public override IdentityError InvalidToken() => T(base.InvalidToken(), "Invalid token.");
        public override IdentityError RecoveryCodeRedemptionFailed() => T(base.RecoveryCodeRedemptionFailed(), "Recovery code redemption failed.");
        public override IdentityError LoginAlreadyAssociated() => T(base.LoginAlreadyAssociated(), "A user with this login already exists.");
        public override IdentityError InvalidUserName(string? userName) => T(base.InvalidUserName(userName), "Username '{0}' is invalid, can only contain letters or digits.", userName ?? "");
        public override IdentityError InvalidEmail(string? email) => T(base.InvalidEmail(email), "Email '{0}' is invalid.", email ?? "");
        public override IdentityError DuplicateUserName(string userName) => T(base.DuplicateUserName(userName), "Username '{0}' is already taken.", userName);
        public override IdentityError DuplicateEmail(string email) => T(base.DuplicateEmail(email), "Email '{0}' is already taken.", email);
        public override IdentityError UserAlreadyHasPassword() => T(base.UserAlreadyHasPassword(), "User already has a password set.");
        public override IdentityError UserLockoutNotEnabled() => T(base.UserLockoutNotEnabled(), "Lockout is not enabled for this user.");
        public override IdentityError PasswordTooShort(int length) => T(base.PasswordTooShort(length), "Passwords must be at least {0} characters.", length);
        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => T(base.PasswordRequiresUniqueChars(uniqueChars), "Passwords must use at least {0} different characters.", uniqueChars);
        public override IdentityError PasswordRequiresNonAlphanumeric() => T(base.PasswordRequiresNonAlphanumeric(), "Passwords must have at least one non alphanumeric character.");
        public override IdentityError PasswordRequiresDigit() => T(base.PasswordRequiresDigit(), "Passwords must have at least one digit ('0'-'9').");
        public override IdentityError PasswordRequiresLower() => T(base.PasswordRequiresLower(), "Passwords must have at least one lowercase ('a'-'z').");
        public override IdentityError PasswordRequiresUpper() => T(base.PasswordRequiresUpper(), "Passwords must have at least one uppercase ('A'-'Z').");
    }
}
