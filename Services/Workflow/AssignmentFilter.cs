using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OpenQMS.Models;

namespace OpenQMS.Services.Workflow
{
    /// <summary>
    /// After every POST to a workflow module, for the record it names ("id"): when the record has moved to another
    /// state, its assignment for the old stage ends (with who signed the step), and the person chosen in the step's
    /// signature dialog ("assignNextTo", optional) is named for the new stage. Works in its own scope, so it never
    /// touches what the step itself saved; a problem here never undoes or blocks the step.
    /// </summary>
    public class AssignmentFilter : IAsyncActionFilter
    {
        public const string NextField = "assignNextTo";
        public const string MessageKey = "AssignmentMessage";

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AssignmentFilter> _log;
        private readonly ITempDataDictionaryFactory _tempData;

        public AssignmentFilter(IServiceScopeFactory scopes, ILogger<AssignmentFilter> log, ITempDataDictionaryFactory tempData)
        {
            _scopes = scopes;
            _log = log;
            _tempData = tempData;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var module = context.RouteData.Values["controller"] as string;
            if (!HttpMethods.IsPost(context.HttpContext.Request.Method) || !TaskAssignmentService.IsWorkflowModule(module)
                || RecordId(context) is not int id)
            {
                await next();
                return;
            }

            var before = await StateAsync(module!, id);
            var executed = await next();
            if (executed.Exception != null && !executed.ExceptionHandled)
                return;

            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<TaskAssignmentService>();
                var record = await svc.LoadRecordAsync(module!, id);
                if (record == null || before == null)
                    return;
                var after = TaskAssignmentService.StateOf(record);
                if (after == before)
                    return;

                var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var signer = await users.GetUserAsync(context.HttpContext.User);
                var by = signer?.UserName ?? context.HttpContext.User.Identity?.Name ?? "system";
                await svc.EndMovedOnAsync(module!, id, after, by);

                var form = context.HttpContext.Request.HasFormContentType ? context.HttpContext.Request.Form : null;
                if (form != null && int.TryParse(form[NextField], out var assignee) && assignee > 0)
                {
                    // The signer just did the work the next step reviews, so they are never named for it.
                    var why = await svc.AssignAsync(module!, record, assignee, null, null, by, alsoExclude: signer);
                    if (why != null)
                        _tempData.GetTempData(context.HttpContext)[MessageKey] = "warning|" + why;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Task assignment after {Module} #{Id} failed; the workflow step itself was saved.", module, id);
            }
        }

        private static int? RecordId(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("id", out var v) && v is int i)
                return i;
            return int.TryParse(context.RouteData.Values["id"] as string, out var r) ? r : null;
        }

        private async Task<int?> StateAsync(string module, int id)
        {
            using var scope = _scopes.CreateScope();
            var record = await scope.ServiceProvider.GetRequiredService<TaskAssignmentService>().LoadRecordAsync(module, id);
            return record == null ? null : TaskAssignmentService.StateOf(record);
        }
    }
}
