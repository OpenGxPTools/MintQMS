using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OpenQMS.Authorization
{
    /// <summary>
    /// Blocks all write operations (POST/PUT/DELETE/PATCH) for users in the ReadOnly role.
    /// This role is intended for internal auditors and authorised non quality/regulatory personnel who need
    /// view-only access across all modules without the ability to create or modify records.
    /// </summary>
    public class ReadOnlyActionFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var method = context.HttpContext.Request.Method;
            var isWriteRequest = HttpMethods.IsPost(method) ||
                                 HttpMethods.IsPut(method) ||
                                 HttpMethods.IsDelete(method) ||
                                 HttpMethods.IsPatch(method);

            if (isWriteRequest && context.HttpContext.User.IsInRole(Constants.ReadOnlyRole))
            {
                context.Result = new ForbidResult();
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
