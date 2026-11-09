using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Decides each request's interface language from System Settings:
    ///   * Site Language — the language for everyone (default English);
    ///   * Let Users Choose Their Language — only when on, a user's own choice (the language cookie
    ///     set by the language menu) is honoured, and only for an installed language.
    /// Settings are read per request (like PasswordExpiryMiddleware), so an administrator's change
    /// applies on the next page without a restart. If the settings cannot be read, English is used.
    /// </summary>
    public class SiteLanguageCultureProvider : RequestCultureProvider
    {
        internal const string ItemKey = "MintQMS.UserMayChooseLanguage";

        public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext http)
        {
            var languages = http.RequestServices.GetRequiredService<Languages>();
            string site = Languages.Default;
            bool userMayChoose = false;
            try
            {
                var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
                var s = await db.SystemSettings.AsNoTracking().Where(x => x.Id == 1)
                    .Select(x => new { x.SiteLanguage, x.AllowUserLanguageChoice }).FirstOrDefaultAsync();
                site = languages.Find(s?.SiteLanguage) ?? Languages.Default;
                userMayChoose = s?.AllowUserLanguageChoice == true && languages.OffersChoice;
            }
            catch (Exception ex)
            {
                http.RequestServices.GetService<ILogger<SiteLanguageCultureProvider>>()?
                    .LogWarning(ex, "Language settings could not be read; using English.");
            }
            http.Items[ItemKey] = userMayChoose;

            var chosen = site;
            if (userMayChoose && http.Request.Cookies.TryGetValue(Languages.CookieName, out var cookie))
            {
                var parsed = CookieRequestCultureProvider.ParseCookieValue(cookie);
                var own = languages.Find(parsed?.UICultures.FirstOrDefault().Value);
                if (own != null) chosen = own;
            }
            // Only the UI culture is meaningful: the format culture is pinned in Program.cs, so this
            // (unsupported) culture value falls back to it and dates/numbers never change.
            return new ProviderCultureResult(chosen, chosen);
        }

        /// <summary>True when this request may show the language menu (set by the provider above).</summary>
        public static bool UserMayChoose(HttpContext http) =>
            http.Items.TryGetValue(ItemKey, out var v) && v is true;
    }
}
