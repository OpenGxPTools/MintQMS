namespace OpenQMS.Authorization
{
    /// <summary>
    /// Marks an action that a ReadOnly user may POST to because it changes no quality record —
    /// only that user's own display preference (e.g. the interface language).
    /// <see cref="ReadOnlyActionFilter"/> lets these through; every other write is still refused.
    /// Do not put this on anything that writes to the database.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AllowForReadOnlyAttribute : Attribute { }
}
