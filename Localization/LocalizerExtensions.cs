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

        public static string Role(this IStringLocalizer localizer, string? roleName)
        {
            var english = EnumHelper.RoleName(roleName);
            return english.Length == 0 ? english : localizer[english].Value;
        }
    }
}
