using System.Globalization;
using System.Net;
using OpenQMS.Models;
using OpenQMS.Services.Workflow;
using static OpenQMS.Localization.RecordText;

namespace OpenQMS.Services.Email
{
    /// <summary>
    /// The e-mail telling a person a task was assigned to them, in the SITE language. It carries only what is needed to
    /// find the task — type of record, number, next step, stage, due date, who assigned it, their note and a link — and
    /// no record content (no title, description, product, patient or customer details).
    /// </summary>
    public static class AssignmentEmail
    {
        /// <summary>Why the person is told: named now, named instead of someone, a planned step has started, or the record
        /// came back to a step they did before.</summary>
        public enum Kind { Assigned, Reassigned, Started, Returned }

        public static EmailOutboxMessage Compose(string module, object record, int stageCode, AppUser assignee, string assignerName,
                                                 DateTime? dueDate, string? note, Kind kind, SystemSettings settings)
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo(string.IsNullOrWhiteSpace(settings.SiteLanguage) ? "en" : settings.SiteLanguage);
            try
            {
                var m = WorkflowRules.Modules[module];
                var recordId = (int)record.GetType().GetProperty("Id")!.GetValue(record)!;
                var number = record.GetType().GetProperty(m.NumberField)?.GetValue(record)?.ToString() ?? recordId.ToString();
                var stage = RD((Enum)record.GetType().GetProperty("Status")!.GetValue(record)!) ?? "";
                var steps = R(TaskAssignmentService.TaskAt(module, stageCode, record));
                var type = R(m.DisplayName);
                var link = string.IsNullOrWhiteSpace(settings.SiteWebAddress)
                    ? null : $"{settings.SiteWebAddress.TrimEnd('/')}/{module}/Details/{recordId}";
                string E(string? s) => WebUtility.HtmlEncode(s ?? "");

                var subject = R("MintQMS: {0} {1} — {2}", type, number, steps);
                var rows = new List<(string, string)>
                {
                    (R("Record"), $"{type} {number}"),
                    (R("Task"), steps),
                    (R("Status"), stage),
                    (R("Due Date"), dueDate?.ToString("dd MMM yyyy", CultureInfo.CurrentUICulture) ?? "—"),
                    (R("Assigned by"), assignerName),
                };
                if (!string.IsNullOrWhiteSpace(note))
                    rows.Add((R("Note"), note!));
                var body =
                    $"<p>{E(TaskAssignmentService.DisplayName(assignee))},</p>" +
                    $"<p>{E(kind switch
                    {
                        Kind.Reassigned => R("{0} has made you responsible for a task in MintQMS (reassigned).", assignerName),
                        Kind.Started => R("A task {0} planned for you in MintQMS is ready for you now.", assignerName),
                        Kind.Returned => R("A record has been sent back to a step you did before: the task is yours again in MintQMS."),
                        _ => R("{0} has made you responsible for a task in MintQMS.", assignerName),
                    })}</p>" +
                    "<table cellpadding=\"4\" style=\"border-collapse:collapse\">" +
                    string.Concat(rows.Select(r => $"<tr><td><strong>{E(r.Item1)}</strong></td><td>{E(r.Item2)}</td></tr>")) +
                    "</table>" +
                    (link != null
                        ? $"<p><a href=\"{E(link)}\">{E(R("Open the record"))}</a> — {E(link)}</p>"
                        : $"<p>{E(R("Sign in to MintQMS and open record {0}.", number))}</p>") +
                    $"<p style=\"color:#666;font-size:small\">{E(R("Being named is for accountability: anyone allowed may still do the step. This message contains no record content; sign in to see it. Please do not reply."))}</p>";

                return new EmailOutboxMessage
                {
                    ToAddress = assignee.Email!,
                    Subject = subject.Length > 300 ? subject[..300] : subject,
                    HtmlBody = body,
                    Kind = "TaskAssigned",   // the outbox kind (one for all four reasons)
                    Module = module,
                    RecordId = recordId,
                    CreatedOn = DateTime.UtcNow,
                    NextAttemptOn = DateTime.UtcNow,
                };
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }
    }
}
