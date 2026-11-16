using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenQMS.Services.Workflow
{
    /// <summary>
    /// Who may sign each workflow step, and its four-eyes rule, read once from the embedded
    /// Resources/Workflow/workflow-rules.json. The roles mirror the checks in the controllers (the e2e test
    /// test_four_eyes.py proves they match); they are used to list eligible people, never to grant access.
    /// </summary>
    public static class WorkflowRules
    {
        public sealed class FourEyesRule
        {
            /// <summary>The signer may not be the user recorded in any of these fields.</summary>
            [JsonPropertyName("notBy")] public List<string> NotBy { get; set; } = new();
            /// <summary>A signer who raised the record may sign only if this field holds someone else.</summary>
            [JsonPropertyName("raiserReview")] public string? RaiserReview { get; set; }
        }

        public sealed class Step
        {
            [JsonPropertyName("action")] public string Action { get; set; } = "";
            [JsonPropertyName("from")] public int From { get; set; }
            [JsonPropertyName("to")] public int To { get; set; }
            [JsonPropertyName("roles")] public List<string> Roles { get; set; } = new();
            /// <summary>The user recorded in this field may also sign, whatever their role (e.g. the author).</summary>
            [JsonPropertyName("alsoUserIn")] public string? AlsoUserIn { get; set; }
            [JsonPropertyName("fourEyes")] public FourEyesRule? FourEyes { get; set; }
            /// <summary>"task": a forward step people are assigned to; "other": send back, re-open, suspend…</summary>
            [JsonPropertyName("kind")] public string Kind { get; set; } = "other";
            /// <summary>The step applies only when this record field has this value (e.g. qualification required).</summary>
            [JsonPropertyName("onlyIf")] public Condition? OnlyIf { get; set; }
            /// <summary>The step's button text (an English translation key), for task lists.</summary>
            [JsonPropertyName("label")] public string Label { get; set; } = "";

            public bool IsTask => Kind == "task";

            public bool AppliesTo(object record) =>
                OnlyIf is not { } c
                || Equals(record.GetType().GetProperty(c.Field)?.GetValue(record), c.EqualsValue.ValueKind switch
                   {
                       JsonValueKind.True => true,
                       JsonValueKind.False => false,
                       JsonValueKind.Number => (object)c.EqualsValue.GetInt32(),
                       _ => c.EqualsValue.GetString(),
                   });
        }

        public sealed class Condition
        {
            [JsonPropertyName("field")] public string Field { get; set; } = "";
            [JsonPropertyName("equals")] public JsonElement EqualsValue { get; set; }
        }

        public sealed class Module
        {
            [JsonPropertyName("module")] public string Name { get; set; } = "";
            [JsonPropertyName("entity")] public string Entity { get; set; } = "";
            [JsonPropertyName("raisedBy")] public string RaisedBy { get; set; } = "";
            [JsonPropertyName("auditTable")] public string AuditTable { get; set; } = "";
            [JsonPropertyName("auditKey")] public string AuditKey { get; set; } = "";
            /// <summary>How lists show a record: its number, its title and when it was raised.</summary>
            [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
            [JsonPropertyName("numberField")] public string NumberField { get; set; } = "";
            [JsonPropertyName("titleField")] public string TitleField { get; set; } = "";
            [JsonPropertyName("raisedOnField")] public string RaisedOnField { get; set; } = "";
            [JsonPropertyName("steps")] public List<Step> Steps { get; set; } = new();
        }

        private sealed class File
        {
            [JsonPropertyName("modules")] public List<Module> Modules { get; set; } = new();
        }

        private static readonly Lazy<IReadOnlyDictionary<string, Module>> _modules = new(Load);

        public static IReadOnlyDictionary<string, Module> Modules => _modules.Value;

        /// <summary>The step a module's action performs from a given state, or null.</summary>
        public static Step? Find(string module, string action, int fromState) =>
            Modules.TryGetValue(module, out var m)
                ? m.Steps.FirstOrDefault(s => s.Action == action && s.From == fromState)
                : null;

        /// <summary>The forward steps (tasks) a record can take from its current state.</summary>
        public static IReadOnlyList<Step> TasksFrom(string module, int state, object record) =>
            Modules.TryGetValue(module, out var m)
                ? m.Steps.Where(s => s.IsTask && s.From == state && s.AppliesTo(record)).ToList()
                : Array.Empty<Step>();

        private static IReadOnlyDictionary<string, Module> Load()
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("workflow-rules.json", StringComparison.Ordinal));
            using var stream = asm.GetManifestResourceStream(name)!;
            var file = JsonSerializer.Deserialize<File>(stream)
                       ?? throw new InvalidOperationException("workflow-rules.json could not be read.");
            return file.Modules.ToDictionary(m => m.Name);
        }
    }
}
