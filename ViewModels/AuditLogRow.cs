namespace OpenQMS.ViewModels;

public sealed record AuditLogRow(
    DateTime PerformedOn,
    string   PerformedBy,
    string   Action,
    string?  FromStatus,
    string?  ToStatus,
    string?  Notes);
