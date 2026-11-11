namespace OpenQMS.Services
{
    /// <summary>
    /// The outcome of a workflow check. A refusal keeps its message twice: in English (<see cref="Error"/>,
    /// for logs and tests) and as an English template with {0}, {1}… placeholders plus its values, so the
    /// screen can show it in the user's language (<c>this.TError(result)</c> in a controller).
    /// Values may be enums (shown by their name), string[] (shown as a list) or dates (shown as day month year).
    /// </summary>
    public abstract class WorkflowResultBase
    {
        public bool     Success       { get; init; }
        public string?  Error         { get; init; }
        public string   ErrorTemplate { get; init; } = "";
        public object[] ErrorArgs     { get; init; } = Array.Empty<object>();

        /// <summary>The template filled in, in English.</summary>
        protected static string English(string template, object[] args) =>
            args.Length == 0 ? template : string.Format(System.Globalization.CultureInfo.InvariantCulture, template,
                args.Select(a => a switch
                {
                    Enum e      => EnumHelper.Display(e),
                    string[] xs => string.Join(", ", xs),
                    DateTime d  => d.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    _           => a,
                }).ToArray());
    }
}
