namespace OpenQMS.Models.ViewModels
{
    // Gap 13: Flat projection used by DisposalController to populate the cross-module
    // disposal-eligible list without loading full entity graphs.
    public class DisposalEligibleItem
    {
        public string   Module                     { get; set; } = string.Empty;
        public int      RecordId                   { get; set; }
        public string   RecordReference            { get; set; } = string.Empty;
        public string   Title                      { get; set; } = string.Empty;
        public string   TerminalStatus             { get; set; } = string.Empty;
        public DateTime TerminalDate               { get; set; }
        public DateTime RetainUntil                { get; set; }

        // Retention hold fields
        public bool               RetentionExtended           { get; set; }
        public RetentionHoldReason? RetentionExtendedReason   { get; set; }
        public string?            RetentionExtendedComments   { get; set; }
        public string?            RetentionExtendedBy         { get; set; }
        public DateTime?          RetentionExtendedOn         { get; set; }
        public DateTime?          RetentionHoldCooldownUntil  { get; set; }

        // Derived helpers for the view
        public bool IsEligibleForDisposal =>
            !RetentionExtended &&
            (RetentionHoldCooldownUntil == null || RetentionHoldCooldownUntil <= DateTime.UtcNow);

        public bool CooldownActive =>
            !RetentionExtended &&
            RetentionHoldCooldownUntil.HasValue &&
            RetentionHoldCooldownUntil > DateTime.UtcNow;
    }
}
