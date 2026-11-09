using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    // Gap 6: rejects a new password if it matches any of the user's recent stored hashes.
    public class PasswordReuseValidator : IPasswordValidator<AppUser>
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<AppUser> _hasher;
        private readonly Microsoft.Extensions.Localization.IStringLocalizer<OpenQMS.Localization.SharedResource>? _l;

        public PasswordReuseValidator(ApplicationDbContext context, IPasswordHasher<AppUser> hasher,
            Microsoft.Extensions.Localization.IStringLocalizer<OpenQMS.Localization.SharedResource>? l = null)
        {
            _context = context;
            _hasher  = hasher;
            _l       = l;
        }

        public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string password)
        {
            var settings = await _context.SystemSettings.FindAsync(1);
            int historyCount = settings?.PasswordHistoryCount ?? 0;

            if (historyCount == 0)
                return IdentityResult.Success;

            var history = await _context.PasswordHistory
                .Where(h => h.UserId == user.Id)
                .OrderByDescending(h => h.CreatedOn)
                .Take(historyCount)
                .ToListAsync();

            foreach (var entry in history)
            {
                var result = _hasher.VerifyHashedPassword(user, entry.PasswordHash, password);
                if (result != PasswordVerificationResult.Failed)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code        = "PasswordReuse",
                        Description = OpenQMS.Localization.LocalizedIdentityErrorDescriber.English || _l == null
                            ? $"You cannot reuse any of your last {historyCount} passwords."
                            : _l["You cannot reuse any of your last {0} passwords.", historyCount].Value
                    });
                }
            }

            return IdentityResult.Success;
        }
    }
}
