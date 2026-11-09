using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using OpenQMS.Authorization;
using OpenQMS.Localization;

namespace OpenQMS.Controllers
{
    /// <summary>
    /// Remembers the interface language chosen from the language menu, in a browser cookie.
    /// Open to everyone (the menu is also on the sign-in page) and to ReadOnly users: it changes
    /// a display preference, never a record, so it is not audited.
    /// </summary>
    [AllowAnonymous]
    public class LanguageController : Controller
    {
        private readonly Languages _languages;

        public LanguageController(Languages languages) => _languages = languages;

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowForReadOnly]
        public IActionResult Set(string? culture, string? returnUrl)
        {
            var lang = _languages.Find(culture);
            if (lang == null || !_languages.MenuShown(HttpContext))
                return BadRequest();   // only an installed language, and only when users may choose

            Response.Cookies.Append(
                Languages.CookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,   // a preference the user asked for, not tracking
                    Path = "/",
                });

            // Never redirect off-site.
            return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
    }
}
