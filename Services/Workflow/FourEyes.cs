using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OpenQMS.Data;
using OpenQMS.Localization;
using OpenQMS.Models;

namespace OpenQMS.Services.Workflow
{
    /// <summary>
    /// The four-eyes rule on a signed step: a review, acceptance, approval, closure or certification must be signed
    /// by someone who did not do the work being reviewed. Checked on the step itself (assignment is never a lock),
    /// from the rules in workflow-rules.json, while System Settings → Enforce Four-Eyes Rule is on.
    /// </summary>
    public static class FourEyes
    {
        // Why the signer is refused, per field ("Module.Field" first, then "Field"). The texts are in a
        // /* i18n */ list so tools/i18n.py collects them for translation; ReasonKeys[i] goes with ReasonTexts[i].
        private static readonly string[] ReasonKeys =
        {
            "EvaluatedBy", "ResolvedBy", "Complaints.ActionBy", "AssessedBy", "Suppliers.AssessedBy", "ImplementedBy",
            "Audits.ConductedBy", "Audits.ReviewedBy", "Risks.ReviewedBy", "Maintenances.PerformedBy",
            "AppDocuments.AuthoredBy", "raiser",
        };
        private static readonly string[] ReasonTexts = new[] { /* i18n */
            "you submitted the evaluation", "you submitted the resolution", "you submitted the evaluation",
            "you submitted the assessment", "you started the assessment", "you submitted the implementation",
            "you started the audit", "you submitted the report for review", "you submitted the mitigation for review",
            "you started the work", "you are the author of this document",
            "you raised this record and its acceptance was not signed by someone else",
        };
        private static readonly Dictionary<string, string> Reasons =
            ReasonKeys.Zip(ReasonTexts).ToDictionary(p => p.First, p => p.Second);

        /// <summary>
        /// The refusal message (in the user's language) when the current user may not sign this step of this record
        /// under the four-eyes rule; null when the step may be signed. Call it after the signature has been verified
        /// and before the record is changed.
        /// </summary>
        public static async Task<string?> RefusalAsync(this Controller controller, string module, string action, object record)
        {
            var step = WorkflowRules.Find(module, action, StateOf(record));
            if (step?.FourEyes is not { } rule)
                return null;

            var services = controller.HttpContext.RequestServices;
            var settings = await services.GetRequiredService<ApplicationDbContext>().SystemSettings.FindAsync(1);
            if (!(settings?.EnforceFourEyes ?? true))
                return null;

            var user = await services.GetRequiredService<UserManager<AppUser>>().GetUserAsync(controller.User);
            if (user == null)
                return null;   // the signature check before this has already refused an unknown user

            foreach (var field in rule.NotBy)
                if (IsUser(Value(record, field), user))
                    return Message(controller, Reasons.GetValueOrDefault($"{module}.{field}") ?? Reasons[field]);

            if (rule.RaiserReview is { } review
                && WorkflowRules.Modules[module].RaisedBy is { Length: > 0 } raisedBy
                && IsUser(Value(record, raisedBy), user))
            {
                var reviewer = Value(record, review);
                if (string.IsNullOrWhiteSpace(reviewer) || IsUser(reviewer, user))
                    return Message(controller, Reasons["raiser"]);
            }
            return null;
        }

        private static string Message(Controller c, string reason)
        {
            var L = c.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            return L["Four-eyes rule: {0}, so another person must sign this step.", L[reason].Value].Value;
        }

        private static int StateOf(object record) =>
            Convert.ToInt32(record.GetType().GetProperty("Status")!.GetValue(record));

        private static string? Value(object record, string field) =>
            record.GetType().GetProperty(field)?.GetValue(record) as string;

        // "…By" fields hold the user name (= e-mail in MintQMS); some modules store the e-mail typed at signing.
        private static bool IsUser(string? value, AppUser user) =>
            !string.IsNullOrWhiteSpace(value)
            && (string.Equals(value.Trim(), user.UserName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.Trim(), user.Email, StringComparison.OrdinalIgnoreCase));
    }
}
