using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    // Gap 11: manages active session records and detects concurrent logins.
    // A lightweight cookie (qms-sid) carries the session token so the correct
    // row can be removed on logout.
    public class SessionTrackingService
    {
        private readonly ApplicationDbContext _context;
        private readonly bool _isProduction;
        private const string CookieName = "qms-sid";

        // Sessions older than this are considered stale and pruned at login time.
        // Should exceed the app's max session lifetime (8 h cookie + idle timeout buffer).
        private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(9);

        public SessionTrackingService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _isProduction = env.IsProduction();
        }

        // Called after a successful login.
        // Returns a list of pre-existing active sessions for the user (empty = no concurrent login).
        // When blockIfConcurrent is true, the new session is NOT registered if concurrent sessions
        // already exist — the caller is responsible for signing the user out in that case.
        public async Task<IReadOnlyList<ActiveSession>> RegisterAsync(
            int userId,
            string ipAddress,
            string? userAgent,
            HttpResponse response,
            bool blockIfConcurrent = false)
        {
            // Prune stale sessions for this user first
            var cutoff = DateTime.UtcNow - StaleAfter;
            var stale = await _context.ActiveSession
                .Where(s => s.UserId == userId && s.LoggedInAt < cutoff)
                .ToListAsync();
            if (stale.Count > 0)
                _context.ActiveSession.RemoveRange(stale);

            // Capture any remaining (genuinely concurrent) sessions before inserting the new one
            var concurrent = await _context.ActiveSession
                .Where(s => s.UserId == userId)
                .ToListAsync();

            // If blocking and a session already exists, save the pruning work and return without registering
            if (blockIfConcurrent && concurrent.Count > 0)
            {
                if (stale.Count > 0)
                    await _context.SaveChangesAsync();
                return concurrent;
            }

            // Register the new session
            var token = Guid.NewGuid().ToString();
            _context.ActiveSession.Add(new ActiveSession
            {
                UserId     = userId,
                SessionToken = token,
                LoggedInAt = DateTime.UtcNow,
                IpAddress  = ipAddress,
                UserAgent  = userAgent
            });

            await _context.SaveChangesAsync();

            // Set the session cookie so we can find this row on logout.
            // Secure flag mirrors the auth cookie policy: required in production,
            // relaxed in development so HTTP access works without a trusted dev cert.
            response.Cookies.Append(CookieName, token, new CookieOptions
            {
                HttpOnly  = true,
                Secure    = _isProduction,
                SameSite  = SameSiteMode.Strict,
                Expires   = DateTimeOffset.UtcNow.Add(StaleAfter)
            });

            return concurrent;
        }

        // Called on logout — removes the session row and clears the cookie.
        public async Task RemoveAsync(HttpRequest request, HttpResponse response)
        {
            var token = request.Cookies[CookieName];
            if (!string.IsNullOrEmpty(token))
            {
                var session = await _context.ActiveSession
                    .FirstOrDefaultAsync(s => s.SessionToken == token);
                if (session != null)
                {
                    _context.ActiveSession.Remove(session);
                    await _context.SaveChangesAsync();
                }
                response.Cookies.Delete(CookieName);
            }
        }
    }
}
