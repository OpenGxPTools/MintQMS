// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Services;

namespace OpenQMS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly SessionTrackingService _sessionTracking;

        public LoginModel(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ILogger<LoginModel> logger, ApplicationDbContext context, SessionTrackingService sessionTracking)
        {
            _userManager      = userManager;
            _signInManager    = signInManager;
            _logger           = logger;
            _context          = context;
            _sessionTracking  = sessionTracking;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            // Already authenticated? Don't show the login form — send them into the app
            // (prevents the confusing "logged-in user looking at the login page" state).
            if (_signInManager.IsSignedIn(User))
            {
                return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : Url.Content("~/"));
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true

                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null)
                {
                    await WriteAuditAsync(Input.Email, "Failed Login");
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }

                var result = await _signInManager.PasswordSignInAsync(user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                    var ua = HttpContext.Request.Headers.UserAgent.ToString();

                    // blockIfConcurrent: prevent simultaneous sessions from different browsers/devices
                    var concurrent = await _sessionTracking.RegisterAsync(user.Id, ip, ua, Response, blockIfConcurrent: true);
                    if (concurrent.Count > 0)
                    {
                        // Sign out the new login — the existing session takes precedence
                        await _signInManager.SignOutAsync();
                        _context.LoginAuditLog.Add(new LoginAuditLog
                        {
                            UserName  = user.UserName ?? Input.Email,
                            Action    = "Login Blocked — Concurrent Session",
                            IpAddress = ip,
                            UserAgent = ua,
                            Timestamp = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                        ModelState.AddModelError(string.Empty,
                            "You are already logged in from another location. " +
                            "Please log out there first, or wait for that session to expire.");
                        return Page();
                    }

                    await WriteAuditAsync(user.UserName ?? Input.Email, "Login");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    await WriteAuditAsync(Input.Email, "Locked Out");
                    return RedirectToPage("./Lockout");
                }
                if (result.IsNotAllowed)
                {
                    await WriteAuditAsync(Input.Email, "Failed Login - Email Not Confirmed");
                    ModelState.AddModelError(string.Empty, "Your email address has not been confirmed. Please check your inbox for the confirmation link, or use 'Resend email confirmation' below.");
                    return Page();
                }
                else
                {
                    await WriteAuditAsync(Input.Email, "Failed Login");
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private async Task WriteAuditAsync(string userName, string action)
        {
            _context.LoginAuditLog.Add(new LoginAuditLog
            {
                UserName  = userName ?? string.Empty,
                Action    = action,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }
}
