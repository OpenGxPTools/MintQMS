using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    // Gap 6: records a password change — updates the expiry timestamp on the user
    // and stores the new hash in PasswordHistory, pruning old entries.
    public class PasswordPolicyService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PasswordPolicyService(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task RecordPasswordChangeAsync(AppUser user)
        {
            // Refresh user from the store so we have the latest PasswordHash
            user = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;

            user.PasswordChangedOn = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            _context.PasswordHistory.Add(new PasswordHistory
            {
                UserId      = user.Id,
                PasswordHash = user.PasswordHash!,
                CreatedOn   = DateTime.UtcNow
            });

            // Prune: keep only the most recent N entries for this user
            var settings = await _context.SystemSettings.FindAsync(1);
            int keep = settings?.PasswordHistoryCount ?? 5;
            if (keep > 0)
            {
                var toDelete = await _context.PasswordHistory
                    .Where(h => h.UserId == user.Id)
                    .OrderByDescending(h => h.CreatedOn)
                    .Skip(keep)
                    .ToListAsync();

                if (toDelete.Count > 0)
                    _context.PasswordHistory.RemoveRange(toDelete);
            }

            await _context.SaveChangesAsync();
        }
    }
}
