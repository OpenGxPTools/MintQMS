using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Localization;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    /// <summary>The organisation header printed on an issued document, as it was at the moment of issue.</summary>
    public class IssuedOrgHeader
    {
        public string? Name { get; init; }
        public string? Address { get; init; }
        /// <summary>The logo embedded in the document (data: URI), so a later logo change cannot alter it.</summary>
        public string? LogoDataUri { get; init; }
    }

    /// <summary>Result of reading the current copy of an issued document.</summary>
    public record IssuedCopyResult(IssuedRecordCopy? Copy, bool IntegrityOk);

    /// <summary>
    /// Issued documents (release certificates, training certificates, attendance lists):
    ///   * written in the SITE language at the moment of issue (not the viewer's own language choice);
    ///   * saved once as a complete copy with a SHA-256 checksum (<see cref="IssuedRecordCopy"/>);
    ///   * always shown from that copy, after checking the checksum.
    /// Nothing is ever edited or deleted; reopening a record supersedes its copy.
    /// </summary>
    public class IssuedRecordService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRazorViewEngine _views;
        private readonly ITempDataProvider _tempData;
        private readonly IServiceProvider _services;
        private readonly Languages _languages;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<IssuedRecordService> _log;

        public IssuedRecordService(ApplicationDbContext db, IRazorViewEngine views, ITempDataProvider tempData,
            IServiceProvider services, Languages languages, IWebHostEnvironment env, ILogger<IssuedRecordService> log)
        {
            _db = db; _views = views; _tempData = tempData; _services = services;
            _languages = languages; _env = env; _log = log;
        }

        // ── Language ────────────────────────────────────────────────────────────────────────────

        /// <summary>The site language (System Settings), if installed on this server; otherwise English.</summary>
        public async Task<string> SiteLanguageAsync()
        {
            try
            {
                var code = await _db.SystemSettings.AsNoTracking().Where(s => s.Id == 1)
                    .Select(s => s.SiteLanguage).FirstOrDefaultAsync();
                return _languages.Find(code) ?? Languages.Default;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Site language could not be read; issuing in English.");
                return Languages.Default;
            }
        }

        // ── Organisation header ─────────────────────────────────────────────────────────────────

        public async Task<IssuedOrgHeader> OrgHeaderAsync()
        {
            var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
            return new IssuedOrgHeader
            {
                Name        = string.IsNullOrWhiteSpace(s?.OrganisationName) ? null : s!.OrganisationName,
                Address     = string.IsNullOrWhiteSpace(s?.OrganisationAddress) ? null : s!.OrganisationAddress,
                LogoDataUri = LogoDataUri(s?.LogoPath),
            };
        }

        private string? LogoDataUri(string? logoUrlPath)
        {
            if (string.IsNullOrWhiteSpace(logoUrlPath)) return null;
            try
            {
                var root = Path.GetFullPath(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"));
                var physical = Path.GetFullPath(Path.Combine(root,
                    logoUrlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                // The stored path must stay inside wwwroot (it is admin-set, but never trust a path).
                if (!physical.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return null;
                if (!File.Exists(physical)) return null;
                var type = Path.GetExtension(physical).ToLowerInvariant() switch
                {
                    ".png"            => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif"            => "image/gif",
                    _                 => null
                };
                if (type == null) return null;
                return $"data:{type};base64,{Convert.ToBase64String(File.ReadAllBytes(physical))}";
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Logo could not be embedded in an issued document.");
                return null;
            }
        }

        // ── Rendering ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders a document partial (e.g. "~/Views/Release/_CertificateDocument.cshtml") to HTML in the
        /// given interface language. <paramref name="http"/> may be null (start-up back-fill).
        /// </summary>
        public async Task<string> RenderAsync(string viewPath, object model, string language, HttpContext? http = null)
        {
            var httpContext = http ?? new DefaultHttpContext { RequestServices = _services };
            var actionContext = new ActionContext(httpContext, httpContext.GetRouteData() ?? new RouteData(), new ActionDescriptor());

            var found = _views.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
            if (!found.Success)
                throw new InvalidOperationException($"Issued document view not found: {viewPath}");

            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
            await using var writer = new StringWriter(CultureInfo.InvariantCulture);
            var viewContext = new ViewContext(actionContext, found.View, viewData,
                new TempDataDictionary(httpContext, _tempData), writer, new HtmlHelperOptions());

            // Only the interface (UI) culture changes: dates and numbers keep the pinned server format.
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo(language);
            try
            {
                await found.View.RenderAsync(viewContext);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
            return writer.ToString();
        }

        // ── Copies ──────────────────────────────────────────────────────────────────────────────

        public static string Sha256Hex(string html) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html))).ToLowerInvariant();

        /// <summary>The copy currently in force (newest not superseded), with its checksum verified.</summary>
        public async Task<IssuedCopyResult> GetActiveAsync(string kind, int sourceId)
        {
            var copy = await _db.IssuedRecordCopies.AsNoTracking()
                .Where(c => c.Kind == kind && c.SourceId == sourceId && c.SupersededOn == null)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();
            if (copy == null) return new IssuedCopyResult(null, true);
            var ok = string.Equals(Sha256Hex(copy.Html), copy.Sha256, StringComparison.OrdinalIgnoreCase);
            if (!ok)
                _log.LogError("Issued document {Kind} #{SourceId} (copy {CopyId}) failed its integrity check.",
                    kind, sourceId, copy.Id);
            return new IssuedCopyResult(copy, ok);
        }

        /// <summary>True when a copy is in force — counting copies added or superseded earlier in this
        /// same unit of work (not saved yet), so two calls before one save never issue twice.</summary>
        public async Task<bool> HasActiveAsync(string kind, int sourceId)
        {
            if (_db.IssuedRecordCopies.Local.Any(c => c.Kind == kind && c.SourceId == sourceId && c.Id == 0 && c.SupersededOn == null))
                return true;
            // Tracked rows keep their in-memory values (identity resolution), so a supersede not yet
            // saved is seen here.
            var rows = await _db.IssuedRecordCopies
                .Where(c => c.Kind == kind && c.SourceId == sourceId && c.SupersededOn == null).ToListAsync();
            return rows.Any(c => c.SupersededOn == null);
        }

        /// <summary>Renders and stores a new copy (added to the context; the caller saves).</summary>
        public async Task<IssuedRecordCopy> IssueAsync(string kind, int sourceId, string viewPath, object model,
            string language, string origin, string? by, HttpContext? http = null)
        {
            var html = await RenderAsync(viewPath, model, language, http);
            var copy = new IssuedRecordCopy
            {
                Kind = kind, SourceId = sourceId, Language = language, Html = html, Sha256 = Sha256Hex(html),
                CreatedOn = DateTime.UtcNow, CreatedBy = by, Origin = origin,
            };
            _db.IssuedRecordCopies.Add(copy);
            return copy;
        }

        /// <summary>Marks every copy in force for this source as superseded (the caller saves).</summary>
        public async Task<int> SupersedeAsync(string kind, int sourceId, string reason)
        {
            var active = await _db.IssuedRecordCopies
                .Where(c => c.Kind == kind && c.SourceId == sourceId && c.SupersededOn == null)
                .ToListAsync();
            var now = DateTime.UtcNow;
            foreach (var c in active)
            {
                c.SupersededOn = now;
                c.SupersededReason = reason.Length > 500 ? reason[..500] : reason;
            }
            return active.Count;
        }
    }
}
