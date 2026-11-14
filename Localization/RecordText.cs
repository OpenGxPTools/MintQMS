using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using OpenQMS.Services;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Text in GENERATED RECORDS (exports: detail PDF/CSV, Excel registers, reports, download stamps).
    /// A record is produced in the SITE language — not the viewer's own choice — which
    /// <see cref="RecordLanguageAttribute"/> sets for the action that builds it.
    ///
    ///   R("Label")   a fixed text, translated (tools/i18n.py collects R("…") keys like L["…"])
    ///   RE(value)    an enum value: in English exactly value.ToString() as exports always showed it;
    ///                in another language its translated display name
    ///
    /// In English both return precisely what the code produced before, so English exports are unchanged.
    /// Never use these for anything stored (audit entries, record fields): those stay English.
    /// </summary>
    public static class RecordText
    {
        private static IStringLocalizer? _localizer;

        public static void Init(IServiceProvider services) =>
            _localizer = services.GetRequiredService<IStringLocalizer<SharedResource>>();

        private static bool English =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" || CultureInfo.CurrentUICulture.Name == "";

        public static string R(string english) =>
            English || _localizer == null ? english : _localizer[english].Value;

        public static string R(string english, params object[] args) =>
            English || _localizer == null ? string.Format(english, args) : _localizer[english, args].Value;

        public static string? RE(Enum? value)
        {
            if (value == null) return null;
            if (English || _localizer == null) return value.ToString();
            // A combination of [Flags] values ("Death, Hospitalisation") is translated part by part.
            var type = value.GetType();
            if (type.IsDefined(typeof(FlagsAttribute), false) && !Enum.IsDefined(type, value))
                return string.Join(", ", value.ToString().Split(", ")
                    .Select(p => _localizer[EnumHelper.Display((Enum)Enum.Parse(type, p))].Value));
            return _localizer[EnumHelper.Display(value)].Value;
        }

        /// <summary>An enum value by its [Display] name (falling back to its code name), translated outside English
        /// — for exports that always showed the display name.</summary>
        public static string? RD(Enum? value)
        {
            if (value == null) return null;
            if (English || _localizer == null)
            {
                var attr = value.GetType().GetField(value.ToString())?
                    .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                    .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault();
                return attr?.Name ?? value.ToString();
            }
            return _localizer[EnumHelper.Display(value)].Value;
        }

        /// <summary>An Excel sheet name from a (translated) title: at most 31 characters, none of : \ / ? * [ ].</summary>
        public static string Sheet(string name)
        {
            var clean = Regex.Replace(name, @"[:\\/?*\[\]]", " ").Trim();
            if (clean.Length == 0) clean = "Sheet";
            return clean.Length <= 31 ? clean : clean[..31].TrimEnd();
        }

        /// <summary>A CSV file's bytes: UTF-8 with the byte-order mark, so spreadsheet programs (Excel) read accented
        /// text — translated headers and names such as "José" — correctly instead of garbling it.</summary>
        public static byte[] CsvBytes(string text) =>
            System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(text)).ToArray();

        /// <summary>A CSV header line from column names (each translated, quoted only when needed).</summary>
        public static string CsvHeader(params string[] englishColumns) =>
            string.Join(",", englishColumns.Select(c => CsvCell(R(c))));

        private static string CsvCell(string v) =>
            v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    /// <summary>
    /// Builds the action's output in the SITE language (System Settings) whatever language the user has chosen
    /// for the screens. Put it on every action that produces a record or export file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RecordLanguageAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var issued = context.HttpContext.RequestServices.GetRequiredService<IssuedRecordService>();
            var language = await issued.SiteLanguageAsync();
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo(language);
            try
            {
                await next();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }
    }
}
