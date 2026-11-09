using System.Globalization;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Dates on SCREENS follow the interface language ("09 nov. 2026", "lunes, 9 de noviembre de 2026").
    /// Use only in views: audit entries, record text, PDFs and exports keep the server's format, and
    /// numbers are never affected (CurrentCulture stays pinned — see Program.cs).
    /// When the interface language is English this returns exactly the culture used before, so English
    /// screens cannot change.
    /// </summary>
    public static class DisplayDates
    {
        private static bool English => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

        /// <summary>Culture for formatting a date shown on screen: x.ToString("dd MMM yyyy", DisplayDates.Culture).</summary>
        public static CultureInfo Culture => English ? CultureInfo.CurrentCulture : CultureInfo.CurrentUICulture;

        /// <summary>A long date with the weekday: English keeps "dddd, d MMMM yyyy"; other languages use their
        /// own long form (e.g. "lunes, 9 de noviembre de 2026").</summary>
        public static string Long(DateTime value) =>
            English ? value.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture)
                    : value.ToString(CultureInfo.CurrentUICulture.DateTimeFormat.LongDatePattern, CultureInfo.CurrentUICulture);
    }
}
