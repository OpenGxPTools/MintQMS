using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OpenQMS
{
    public static class EnumHelper
    {
        public static string Display(Enum? value)
        {
            if (value == null) return string.Empty;
            var field = value.GetType().GetField(value.ToString()!);
            var attr = field?.GetCustomAttribute<DisplayAttribute>();
            if (attr?.Name != null) return attr.Name;
            // Insert space before each uppercase letter that follows a lowercase letter
            return Regex.Replace(value.ToString()!, "(?<=[a-z])(?=[A-Z])", " ");
        }

        // Splits a PascalCase role name string into display-friendly words.
        // e.g. "QualityManager" → "Quality Manager", "ReadOnly" → "Read Only"
        public static string RoleName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
        }
    }
}
