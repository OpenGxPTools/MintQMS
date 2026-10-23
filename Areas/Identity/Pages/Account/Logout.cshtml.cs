// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Services;

namespace OpenQMS.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly SessionTrackingService _sessionTracking;

        public LogoutModel(SignInManager<AppUser> signInManager, ILogger<LogoutModel> logger, ApplicationDbContext context, SessionTrackingService sessionTracking)
        {
            _signInManager   = signInManager;
            _logger          = logger;
            _context         = context;
            _sessionTracking = sessionTracking;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            var userName = User.Identity?.Name ?? string.Empty;
            _context.LoginAuditLog.Add(new LoginAuditLog
            {
                UserName  = userName,
                Action    = "Logout",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await _sessionTracking.RemoveAsync(Request, Response);
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // This needs to be a redirect so that the browser performs a new
                // request and the identity for the user gets updated.
                return RedirectToPage();
            }
        }
    }
}
