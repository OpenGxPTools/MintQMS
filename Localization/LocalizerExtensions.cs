using Microsoft.Extensions.Localization;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Screen-only translation of states, types and role names.
    ///
    /// <see cref="EnumHelper.Display"/> and <see cref="EnumHelper.RoleName"/> stay English on purpose:
    /// they are also used when writing audit-trail entries, PDF reports and messages that become part of
    /// a record. Views call these extensions instead, which look the English name up as the translation
    /// key — so a missing translation still shows the English name, never a code.
    /// </summary>
    public static class LocalizerExtensions
    {
        public static string Enum(this IStringLocalizer localizer, System.Enum? value)
        {
            var english = EnumHelper.Display(value);
            return english.Length == 0 ? english : localizer[english].Value;
        }

        /// <summary>A message passed through TempData, shown in the interface language. Fixed English messages
        /// are the translation key (no translation → English); anything else is shown as it is.</summary>
        public static string Message(this IStringLocalizer localizer, object? value) =>
            value is string s && s.Length > 0 ? localizer[s].Value : value?.ToString() ?? string.Empty;

        public static string Role(this IStringLocalizer localizer, string? roleName)
        {
            var english = EnumHelper.RoleName(roleName);
            return english.Length == 0 ? english : localizer[english].Value;
        }
    }
}
