using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace OpenQMS.Localization
{
    /// <summary>
    /// <c>this.T(englishText)</c> in page models and controllers: the text in the user's interface
    /// language (English when there is no translation). Use it ONLY for text shown to the user —
    /// never for anything stored (audit entries, record fields, file names), which stay English.
    /// tools/i18n.py collects the keys from this.T calls with a string literal.
    /// </summary>
    public static class TranslateExtensions
    {
        public static string T(this PageModel page, string english, params object[] args) =>
            Localizer(page.HttpContext)[english, args].Value;

        public static string T(this Controller controller, string english, params object[] args) =>
            Localizer(controller.HttpContext)[english, args].Value;

        private static IStringLocalizer Localizer(HttpContext http) =>
            http.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
    }
}
