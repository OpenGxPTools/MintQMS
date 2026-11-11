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

        /// <summary>A workflow refusal in the user's language; values that are states, lists or dates are
        /// shown in that language too.</summary>
        public static string TError(this Controller controller, OpenQMS.Services.WorkflowResultBase result) =>
            controller.T(result.ErrorTemplate, result.ErrorArgs.Select(a => a switch
            {
                Enum e      => (object)controller.T(EnumHelper.Display(e)),
                string[] xs => string.Join(", ", xs.Select(x => controller.T(x))),
                DateTime d  => d.ToString("dd MMM yyyy", DisplayDates.Culture),
                _           => a,
            }).ToArray());

        private static IStringLocalizer Localizer(HttpContext http) =>
            http.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
    }
}
