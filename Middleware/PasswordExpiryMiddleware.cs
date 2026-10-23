using Microsoft.AspNetCore.Identity;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Middleware
{
    // Gap 6: on every authenticated request, checks whether the user's password
    // has exceeded the configured expiry threshold and redirects to ChangePassword
    // if so. Paths listed in ExemptPrefixes are never redirected to avoid loops.
    public class PasswordExpiryMiddleware
    {
        private readonly RequestDelegate _next;

        private static readonly string[] ExemptPrefixes =
        [
            "/Identity/Account/Manage/ChangePassword",
            "/Identity/Account/Logout",
            "/Identity/Account/Login",
            "/Identity/Account/ForgotPassword",
            "/Identity/Account/ResetPassword",
            "/_framework",
            "/css",
            "/js",
            "/lib",
            "/favicon",
        ];

        public PasswordExpiryMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext db,
            UserManager<AppUser> userManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var path = context.Request.Path.Value ?? string.Empty;
                bool isExempt = ExemptPrefixes.Any(p =>
                    path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (!isExempt)
                {
                    var settings = await db.SystemSettings.FindAsync(1);
                    int expiryDays = settings?.PasswordExpiryDays ?? 0;

                    if (expiryDays > 0)
                    {
                        var user = await userManager.GetUserAsync(context.User);
                        if (user != null)
                        {
                            var lastChanged = user.PasswordChangedOn ?? DateTime.MinValue;
                            if ((DateTime.UtcNow - lastChanged).TotalDays > expiryDays)
                            {
                                context.Response.Redirect(
                                    "/Identity/Account/Manage/ChangePassword?expired=true");
                                return;
                            }
                        }
                    }
                }
            }

            await _next(context);
        }
    }
}
