using System.Globalization;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Marker type for the single shared translation file (Resources/SharedResource.&lt;lang&gt;.resx).
    /// The English text is the key, so a string that has no translation is shown in English.
    /// </summary>
    public class SharedResource { }

    /// <summary>
    /// The interface languages INSTALLED on this server: appsettings.json "Localization:Languages"
    /// (e.g. ["en", "es", "pt-BR"]). Only the languages MintQMS knows (<see cref="Known"/>) are accepted;
    /// English is always installed. Which one is used — for everyone, or per user — is decided by the
    /// administrator in System Settings (see <see cref="SiteLanguageCultureProvider"/>).
    ///
    /// The language changes the words on screen only. Dates and numbers keep the server's own
    /// format, so a typed number is always read the same way whatever language is chosen.
    /// </summary>
    public class Languages
    {
        public const string Default = "en";

        /// <summary>Languages MintQMS can show, with the name each one has in its own language.</summary>
        public static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English",
            ["es"] = "Español",
            ["pt-BR"] = "Português (Brasil)",
        };

        /// <summary>Cookie that remembers the chosen language in the browser.</summary>
        public const string CookieName = ".MintQMS.Language";

        public IReadOnlyList<string> Enabled { get; }

        public Languages(IConfiguration config)
        {
            var configured = config.GetSection("Localization:Languages").Get<string[]>() ?? Array.Empty<string>();
            var list = new List<string> { Default };
            foreach (var c in configured)
            {
                var match = Known.Keys.FirstOrDefault(k => string.Equals(k, c?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null && !list.Contains(match)) list.Add(match);
            }
            Enabled = list;
        }

        /// <summary>True when more than one language is installed (a precondition for any choice).</summary>
        public bool OffersChoice => Enabled.Count > 1;

        /// <summary>True when this request may show the language menu: installed languages to choose
        /// from AND the administrator allows users to choose (System Settings).</summary>
        public bool MenuShown(HttpContext http) => OffersChoice && SiteLanguageCultureProvider.UserMayChoose(http);

        /// <summary>The enabled language matching <paramref name="code"/> (any case), or null.</summary>
        public string? Find(string? code) =>
            Enabled.FirstOrDefault(l => string.Equals(l, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        public IList<CultureInfo> UiCultures => Enabled.Select(l => new CultureInfo(l)).ToList();
    }
}
