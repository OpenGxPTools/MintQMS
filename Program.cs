/*

This file is part of the OpenQMS.net project(htps://github.com/C-realize/OpenQMS).
Copyright(C) 2022 - 2024  C - realize IT Services SRL (https://www.c-realize.com)

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://www.c-realize.com/contact.  For AGPL licensing, see below.

AGPL:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see http://www.gnu.org/licenses/.
*/

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using OpenQMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Middleware;
using OpenQMS.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    //options.UseSqlServer(connectionString));
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
// Data-protection keys (sign-in cookies, anti-forgery tokens, reset links). By default they live in
// the account's home folder; a service account usually has none, so keys would be lost on every
// restart (everyone signed out). Production sets DataProtectionKeysPath to a persistent folder.
var keysPath = builder.Configuration["DataProtectionKeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("MintQMS");
}

builder.Services.AddDefaultIdentity<AppUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;

    // Password policy
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 4;

    // Lockout policy
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<OpenQMS.Authorization.ReadOnlyActionFilter>();
});
builder.Services.AddRazorPages();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Cookie security
var isDev = builder.Environment.IsDevelopment();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    // In development allow HTTP so the dev cert warning doesn't block login.
    // In production the Secure flag is mandatory.
    options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Configure the 2FA intermediate cookie to match the same HTTP/HTTPS policy as the app cookie.
// Without this, the browser drops the Secure 2FA cookie over HTTP in dev and the 2FA page fails.
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, options =>
{
    options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Validate security stamp on every request so role/permission changes take effect immediately
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// Reverse proxy (nginx/Caddy terminating HTTPS): honour X-Forwarded-For/-Proto so the app sees the
// real scheme and client address — e-mailed links then use https. Only proxies on this host
// (loopback, the default) are trusted; add others via KnownProxies if the proxy runs elsewhere.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// HSTS — 1 year, applies to subdomains
builder.Services.AddHsts(options =>
{
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Email sender
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// PV Services
builder.Services.AddScoped<PVWorkflowService>();
builder.Services.AddScoped<PVRegulatoryService>();
builder.Services.AddScoped<CIOMSFormService>();
builder.Services.AddHostedService<PVDeadlineMonitorService>();

// RegIntel Services
builder.Services.AddScoped<RIWorkflowService>();
builder.Services.AddScoped<RINotificationService>();
builder.Services.AddHostedService<RIDeadlineMonitorService>();

// LicCert Services
builder.Services.AddScoped<LicCertWorkflowService>();
builder.Services.AddScoped<LicCertNotificationService>();
builder.Services.AddHostedService<RenewalLeadTimeMonitorService>();

// Release Services
builder.Services.AddScoped<ReleaseWorkflowService>();
builder.Services.AddScoped<DocumentWorkflowService>();

// Gap 6: password expiry and reuse prevention
builder.Services.AddScoped<PasswordPolicyService>();
builder.Services.AddScoped<IPasswordValidator<AppUser>, PasswordReuseValidator>();

// Gap 11: concurrent login detection
builder.Services.AddScoped<SessionTrackingService>();

// Gap 13: data retention and disposal
builder.Services.AddScoped<RetentionService>();

// Authorization handlers.
builder.Services.AddScoped<IAuthorizationHandler,
                      AuthorAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler,
                      AdministratorAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler,
                      ManagerAuthorizationHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();

    // Seed admin user password: set via Secret Manager for development
    //   dotnet user-secrets set SeedAdminPW <password>
    // or via environment variable SEEDADMINPW for production.
    // If not set, no user is created (safe to omit once admin@trial.local exists in the DB).
    var adminPw = app.Configuration["SeedAdminPW"] ?? "";

    await SeedData.Initialize(services, adminPw);

    // Site time zone for displaying stored UTC timestamps (System Settings; default Australia/Brisbane)
    OpenQMS.SiteTime.Configure(await context.SystemSettings.Select(s => s.TimeZoneId).FirstOrDefaultAsync());

    // Clear all active sessions on startup.
    // qms-sid cookies from any previous run are orphaned once the process restarts,
    // so keeping those rows would permanently block users from logging back in after
    // a crash or restart (the concurrent-session guard would fire on every attempt).
    await context.ActiveSession.ExecuteDeleteAsync();
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

// "AllowedHosts": "*" accepts any Host header, which lets a crafted request put an attacker's
// address into password-reset and confirmation e-mails. Production must name the real host(s).
if (!app.Environment.IsDevelopment() && (app.Configuration["AllowedHosts"] ?? "*").Trim() == "*")
{
    app.Logger.LogWarning("AllowedHosts is \"*\". Set it to this site's host name (e.g. qms.example.com) " +
        "so e-mailed links cannot be redirected to another address. See DEPLOYMENT.md.");
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
// Uploaded files (record attachments/documents) must only be served through the controller
// download actions, which enforce authorisation, audit logging and watermarking. Static-file
// serving runs before authentication, so block /uploads here — except the organisation logo,
// which is shown on certificates and settings pages.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/uploads/logo", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PasswordExpiryMiddleware>();
app.MapControllerRoute(
        name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.Run();

public partial class Program { }
