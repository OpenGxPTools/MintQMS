using System.Reflection;
using System.Text.RegularExpressions;

namespace OpenQMS.Services;

/// <summary>
/// Builds a human-readable change summary for Edit audit log entries.
/// Each changed field is recorded as: FieldName: "old" → "new"
/// Long values are stored in full but displayed truncated in the UI.
/// </summary>
public static class AuditDiffHelper
{
    public static string Build(List<string> parts)
        => parts.Count == 0 ? "Saved with no change to any recorded field." : string.Join(" | ", parts);

    // String fields — normalise whitespace before comparing
    public static void Track(List<string> parts, string label, string? oldVal, string? newVal)
    {
        var o = oldVal?.Trim() ?? "";
        var n = newVal?.Trim() ?? "";
        if (o == n) return;
        parts.Add($"{label}: \"{Fmt(o)}\" → \"{Fmt(n)}\"");
    }

    // Value-type / nullable value-type fields
    public static void Track<T>(List<string> parts, string label, T? oldVal, T? newVal)
        where T : struct, IEquatable<T>
    {
        if (EqualityComparer<T?>.Default.Equals(oldVal, newVal)) return;
        parts.Add($"{label}: \"{oldVal?.ToString() ?? "(none)"}\" → \"{newVal?.ToString() ?? "(none)"}\"");
    }

    // Nullable DateTime with optional format string
    public static void TrackDate(List<string> parts, string label,
        DateTime? oldVal, DateTime? newVal, string fmt = "yyyy-MM-dd")
    {
        var o = oldVal?.ToString(fmt) ?? "";
        var n = newVal?.ToString(fmt) ?? "";
        if (o == n) return;
        parts.Add($"{label}: \"{(o.Length > 0 ? o : "(none)")}\" → \"{(n.Length > 0 ? n : "(none)")}\"");
    }

    // Enum fields stored as nullable
    public static void TrackEnum<T>(List<string> parts, string label, T? oldVal, T? newVal)
        where T : struct, Enum
    {
        if (EqualityComparer<T?>.Default.Equals(oldVal, newVal)) return;
        parts.Add($"{label}: \"{oldVal?.ToString() ?? "(none)"}\" → \"{newVal?.ToString() ?? "(none)"}\"");
    }

    // Non-nullable enum fields
    public static void TrackEnum<T>(List<string> parts, string label, T oldVal, T newVal)
        where T : Enum
    {
        if (oldVal.Equals(newVal)) return;
        parts.Add($"{label}: \"{oldVal}\" → \"{newVal}\"");
    }

    // Linked-record / affected-item collections (many-to-many).
    // Logs which links were added/removed so link-only edits are no longer reported as untracked.
    // resolveNames (optional) is called with ONLY the changed ids, and ONLY when something changed,
    // so a readable name is shown instead of a bare id (falls back to "#id").
    public static void TrackSet(List<string> parts, string label,
        IEnumerable<int>? oldIds, IEnumerable<int>? newIds,
        Func<List<int>, Dictionary<int, string>>? resolveNames = null)
    {
        var oldSet = new HashSet<int>(oldIds ?? Enumerable.Empty<int>());
        var newSet = new HashSet<int>(newIds ?? Enumerable.Empty<int>());
        var added   = newSet.Where(x => !oldSet.Contains(x)).ToList();
        var removed = oldSet.Where(x => !newSet.Contains(x)).ToList();
        if (added.Count == 0 && removed.Count == 0) return;

        var names = resolveNames?.Invoke(added.Concat(removed).ToList()) ?? new Dictionary<int, string>();
        string Name(int id) => names.TryGetValue(id, out var s) && !string.IsNullOrWhiteSpace(s) ? s : $"#{id}";

        var bits = new List<string>();
        if (added.Count   > 0) bits.Add("added "   + string.Join(", ", added.Select(Name)));
        if (removed.Count > 0) bits.Add("removed " + string.Join(", ", removed.Select(Name)));
        parts.Add($"{label}: {string.Join("; ", bits)}");
    }

    // Reflection-based diff for a child / owned entity (e.g. an adverse-event's Reporter, Patient,
    // Suspect Product or Regulatory Report). Compares every simple-typed public property (string, bool,
    // enum, DateTime, numeric — and their nullable forms); navigation properties and collections are
    // skipped. Each change is recorded as "prefix Property: old → new". Pass property names to ignore
    // (keys / owner FKs). A null old object with a non-null new one is reported as "prefix: added".
    public static void TrackObject<T>(List<string> parts, string prefix, T? oldObj, T? newObj,
        params string[] ignore) where T : class
    {
        if (newObj == null) return;
        if (oldObj == null) { parts.Add($"{prefix}: added"); return; }
        var skip = new HashSet<string>(ignore, StringComparer.Ordinal);

        foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || skip.Contains(p.Name)) continue;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            bool simple = t.IsEnum || t == typeof(string) || t == typeof(bool) || t == typeof(DateTime)
                || t == typeof(int) || t == typeof(long) || t == typeof(short)
                || t == typeof(decimal) || t == typeof(double) || t == typeof(float);
            if (!simple) continue;   // navigation property / collection

            var oldV = p.GetValue(oldObj);
            var newV = p.GetValue(newObj);
            var label = $"{prefix} {Spacify(p.Name)}";

            if (t == typeof(string))
            {
                var o = ((string?)oldV)?.Trim() ?? "";
                var n = ((string?)newV)?.Trim() ?? "";
                if (o == n) continue;
                parts.Add($"{label}: \"{Fmt(o)}\" → \"{Fmt(n)}\"");
            }
            else if (t == typeof(DateTime))
            {
                if (Equals(oldV, newV)) continue;
                var o = ((DateTime?)oldV)?.ToString("yyyy-MM-dd") ?? "(none)";
                var n = ((DateTime?)newV)?.ToString("yyyy-MM-dd") ?? "(none)";
                parts.Add($"{label}: \"{o}\" → \"{n}\"");
            }
            else
            {
                if (Equals(oldV, newV)) continue;
                parts.Add($"{label}: \"{oldV?.ToString() ?? "(none)"}\" → \"{newV?.ToString() ?? "(none)"}\"");
            }
        }
    }

    // "IsReportableToTGA" → "Is Reportable To TGA" for readable audit labels.
    private static string Spacify(string s) => Regex.Replace(s, "(?<=[a-z0-9])(?=[A-Z])", " ");

    // Full value stored; the UI layer handles display truncation.
    private static string Fmt(string v) => v.Length == 0 ? "(none)" : v;
}
