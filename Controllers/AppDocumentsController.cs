/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2025  C-realize IT Services SRL (https://www.c-realize.com)

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

using System.Data;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.Navigation;
using OpenQMS.Models.ViewModels;
using OpenQMS.Services;

//**DocumentFormat.OpenXml nuget package**
//using DocumentFormat.OpenXml.Packaging;
//using DocumentFormat.OpenXml.Wordprocessing;

//**Microsoft Word 16.0 Object Library COM reference**
//using Word = Microsoft.Office.Interop.Word;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class AppDocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly IAuthorizationService _authorizationService;
        private readonly DocumentWorkflowService _docWorkflow;
        private readonly ILogger<AppDocumentsController> _logger;
        private readonly IConfiguration _configuration;

        public AppDocumentsController
        (
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<AppRole> roleManager,
            IPasswordHasher<AppUser> passwordHasher,
            IAuthorizationService authorizationService,
            DocumentWorkflowService docWorkflow,
            ILogger<AppDocumentsController> logger,
            IConfiguration configuration
        )
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _passwordHasher = passwordHasher;
            _authorizationService = authorizationService;
            _docWorkflow = docWorkflow;
            _logger = logger;
            _configuration = configuration;
        }

        // GET: AppDocuments/GetDocumentTypes?category=QualitySystem
        [HttpGet]
        public IActionResult GetDocumentTypes(string category)
        {
            if (!Enum.TryParse<AppDocument.DocumentCategory>(category, out var cat))
                return Json(new List<object>());

            var types = _categoryTypeMap.TryGetValue(cat, out var list) ? list : Array.Empty<AppDocument.DocumentType>();
            var result = types.Select(t => new { value = t.ToString(), text = GetDocTypeDisplayName(t) });
            return Json(result);
        }

        // GET: Documents
        public async Task<IActionResult> Index()
        {
            var isAuthorized = User.IsInRole(Constants.QualityManagerRole)
                            || User.IsInRole(Constants.AdministratorsRole)
                            || User.IsInRole(Constants.AuthorisedPersonRole);

            IQueryable<AppDocument> documents;

            if (isAuthorized)
            {
                // Managers/Admins: current working set (unlocked) plus Obsolete for the full register.
                // Locked-Approved docs (with a draft in progress) are hidden — the draft shows instead.
                documents = _context.AppDocument.Where(d => !d.IsDeleted
                    && (!d.IsLocked || d.Status == AppDocument.DocumentStatus.Obsolete));
            }
            else
            {
                // Other users: show Effective and Approved documents, plus their own drafts
                var currentUserName = _userManager.GetUserName(User);
                documents = _context.AppDocument
                    .Where(d => !d.IsDeleted && d.Status != AppDocument.DocumentStatus.Obsolete)
                    .Where(d => d.Status == AppDocument.DocumentStatus.Approved
                             || d.Status == AppDocument.DocumentStatus.Effective
                             || d.AuthoredBy == currentUserName);
            }

            return View(await documents.ToListAsync());
        }

        // GET: Documents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var document = await _context.AppDocument
                .Include(d => d.AuditLogs)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            var isAuthorized = User.IsInRole(Constants.QualityManagerRole)
                            || User.IsInRole(Constants.AdministratorsRole)
                            || User.IsInRole(Constants.AuthorisedPersonRole);
            var currentUserName = _userManager.GetUserName(User);
            if (!isAuthorized
                && currentUserName != document.AuthoredBy
                && document.Status != AppDocument.DocumentStatus.Approved
                && document.Status != AppDocument.DocumentStatus.Effective)
            {
                return Forbid();
            }

            // Version history: all records sharing the same DocumentId, ordered newest first
            // OrderByDescending on decimal not supported by SQLite EF Core — sort client-side
            var versionHistory = await _context.AppDocument
                .Where(d => d.DocumentId == document.DocumentId)
                .ToListAsync();
            ViewBag.VersionHistory = versionHistory.OrderByDescending(d => d.Version).ToList();

            // Trainings auto-generated from this specific document version
            var linkedTrainings = await _context.DocumentTrainingRequirement
                .Where(r => r.DocumentId == document.Id)
                .Include(r => r.Training)
                    .ThenInclude(t => t.Trainees)
                        .ThenInclude(ut => ut.Trainee)
                .OrderByDescending(r => r.CreatedOn)
                .ToListAsync();
            ViewBag.LinkedTrainings = linkedTrainings;

            // Candidate authors for the Reassign Author action (Quality Manager / Administrator only) —
            // active (not locked-out), non read-only users, excluding the current author.
            if (isAuthorized)
            {
                var now = DateTimeOffset.UtcNow;
                var readOnlyUsers = await _userManager.GetUsersInRoleAsync(Constants.ReadOnlyRole);
                var readOnlyIds = readOnlyUsers.Select(u => u.Id).ToHashSet();
                // Fetch users, then filter in memory — the DateTimeOffset lockout comparison is not
                // translatable to SQL by the SQLite provider, so evaluate client-side (the set is small).
                var allUsers = await _userManager.Users.ToListAsync();
                ViewBag.ReassignCandidates = allUsers
                    .Where(u => !readOnlyIds.Contains(u.Id)
                             && (u.LockoutEnd == null || u.LockoutEnd <= now)
                             && u.UserName != document.AuthoredBy)
                    .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id.ToString(),
                        Text = u.FirstName + " " + u.LastName + " (" + u.UserName + ")"
                    })
                    .ToList();
            }

            ViewBag.ActiveTab = TempData["ActiveTab"]?.ToString() ?? "overview";

            return View(document);
        }

        public async Task<IActionResult> DownloadFile(int id)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            // Critical #2: enforce same access control as Details GET
            var dlAuthorized = User.IsInRole(Constants.QualityManagerRole)
                            || User.IsInRole(Constants.AdministratorsRole)
                            || User.IsInRole(Constants.AuthorisedPersonRole);
            var dlUser = _userManager.GetUserName(User);
            if (!dlAuthorized
                && dlUser != document.AuthoredBy
                && document.Status != AppDocument.DocumentStatus.Approved
                && document.Status != AppDocument.DocumentStatus.Effective)
            {
                return Forbid();
            }

            //Dowload from filesystem
            //var memory = new MemoryStream();
            //using (var stream = new FileStream(document.FilePath, FileMode.Open))
            //{
            //    await stream.CopyToAsync(memory);
            //}
            //memory.Position = 0;

            if (document.Content == null || document.Content.Length == 0)
            {
                return NotFound();
            }

            // EU Annex 11 §4.2 / 21 CFR Part 11 §11.10(a): verify file integrity on retrieval
            if (!string.IsNullOrEmpty(document.FileHash))
            {
                var currentHash = OpenQMS.Services.FileHashHelper.ComputeSha256(document.Content);
                if (!string.Equals(currentHash, document.FileHash, StringComparison.OrdinalIgnoreCase))
                {
                    _context.DocumentAuditLog.Add(new DocumentAuditLog
                    {
                        AppDocumentId = document.Id,
                        Action        = "Integrity Check Failed",
                        FromStatus    = document.Status.ToString(),
                        ToStatus      = document.Status.ToString(),
                        PerformedBy   = _userManager.GetUserName(User) ?? "Unknown",
                        PerformedOn   = DateTime.UtcNow,
                        Notes         = "File hash mismatch detected on download. File may have been tampered with."
                    });
                    await _context.SaveChangesAsync();
                    TempData["IntegrityWarning"] = $"WARNING: The file '{document.Title}' failed its integrity check. The stored hash does not match. This has been logged. Contact your system administrator.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action        = "Downloaded",
                FromStatus    = document.Status.ToString(),
                ToStatus      = document.Status.ToString(),
                PerformedBy   = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn   = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var downloadBytes = document.Content;
            if (string.Equals(document.FileExtension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var settings = await _context.SystemSettings.FindAsync(1);
                var controlSheet = PdfReportService.GenerateDocumentControlSheet(
                    document,
                    settings?.OrganisationName,
                    settings?.OrganisationAddress,
                    PdfReportService.ResolveLogoPath(settings?.LogoPath));
                downloadBytes = PdfReportService.AppendToPdf(document.Content, controlSheet);
            }

            var wmCtx = new WatermarkContext(
                WatermarkKind.Document, document.Status,
                "Document", document.Id.ToString(), document.Version.ToString("0.00"),
                dlUser ?? "Unknown", DateTime.UtcNow,
                DocNumber: document.DocumentId,
                DocTypeCode: document.DocType.HasValue ? GetDocTypeShortCode(document.DocType.Value) : null,
                DocTitle: document.Title);
            downloadBytes = WatermarkService.Apply(downloadBytes, document.FileExtension, wmCtx);

            return File(downloadBytes, document.FileType ?? "application/octet-stream", document.Title + document.FileExtension);
        }

        // Working Copy: a clean, externally-issuable copy of an Effective Form/Template.
        // No control sheet and no internal approval trail — only a discreet right-margin
        // "Uncontrolled copy" mark (placed so it won't collide with the attachment watermark
        // if the completed copy is later re-uploaded). The act of issuing is still audited.
        public async Task<IActionResult> DownloadWorkingCopy(int id)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            // Working copies are only for Forms and Templates, current (Effective) version only.
            var isFormOrTemplate = document.DocType == AppDocument.DocumentType.Form
                                || document.DocType == AppDocument.DocumentType.Template;
            if (!isFormOrTemplate || document.Status != AppDocument.DocumentStatus.Effective)
            {
                TempData["WorkflowError"] = "A working copy is only available for an Effective Form or Template.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Same access control as DownloadFile.
            var dlAuthorized = User.IsInRole(Constants.QualityManagerRole)
                            || User.IsInRole(Constants.AdministratorsRole)
                            || User.IsInRole(Constants.AuthorisedPersonRole);
            var dlUser = _userManager.GetUserName(User);
            if (!dlAuthorized
                && dlUser != document.AuthoredBy
                && document.Status != AppDocument.DocumentStatus.Approved
                && document.Status != AppDocument.DocumentStatus.Effective)
            {
                return Forbid();
            }

            if (document.Content == null || document.Content.Length == 0) return NotFound();

            var version  = document.Version.ToString("0.00");
            var markText = $"Uncontrolled copy · v{version} · MintQMS";
            var bytes    = WatermarkService.ApplyWorkingCopyMark(document.Content, document.FileExtension, markText);

            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action        = "Working Copy Downloaded",
                FromStatus    = document.Status.ToString(),
                ToStatus      = document.Status.ToString(),
                PerformedBy   = dlUser ?? "Unknown",
                PerformedOn   = DateTime.UtcNow,
                Notes         = $"Clean working copy issued for external use (v{version})."
            });
            await _context.SaveChangesAsync();

            var fileName = $"{document.Title} v{version}{document.FileExtension}";
            return File(bytes, document.FileType ?? "application/octet-stream", fileName);
        }

        public async Task<IActionResult> ViewFile(int id)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            var authorized = User.IsInRole(Constants.QualityManagerRole)
                          || User.IsInRole(Constants.AdministratorsRole)
                          || User.IsInRole(Constants.AuthorisedPersonRole);
            var userName = _userManager.GetUserName(User);
            if (!authorized
                && userName != document.AuthoredBy
                && document.Status != AppDocument.DocumentStatus.Approved
                && document.Status != AppDocument.DocumentStatus.Effective)
            {
                return Forbid();
            }

            if (document.Content == null || document.Content.Length == 0) return NotFound();

            if (!string.IsNullOrEmpty(document.FileHash))
            {
                var currentHash = OpenQMS.Services.FileHashHelper.ComputeSha256(document.Content);
                if (!string.Equals(currentHash, document.FileHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Hash mismatch — most likely caused by PDF signing without updating the stored hash.
                    // Correct the stored hash and log the correction so it is auditable.
                    document.FileHash = currentHash;
                    _context.AppDocument.Update(document);
                    _context.DocumentAuditLog.Add(new DocumentAuditLog
                    {
                        AppDocumentId = document.Id,
                        Action        = "File Hash Corrected",
                        FromStatus    = document.Status.ToString(),
                        ToStatus      = document.Status.ToString(),
                        PerformedBy   = userName ?? "Unknown",
                        PerformedOn   = DateTime.UtcNow,
                        Notes         = "Stored hash did not match content (likely caused by PDF signing without hash update). Hash recalculated and corrected automatically."
                    });
                    await _context.SaveChangesAsync();
                }
            }

            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action        = "Viewed",
                FromStatus    = document.Status.ToString(),
                ToStatus      = document.Status.ToString(),
                PerformedBy   = userName ?? "Unknown",
                PerformedOn   = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var viewBytes = document.Content;
            if (string.Equals(document.FileExtension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var settings = await _context.SystemSettings.FindAsync(1);
                var controlSheet = PdfReportService.GenerateDocumentControlSheet(
                    document,
                    settings?.OrganisationName,
                    settings?.OrganisationAddress,
                    PdfReportService.ResolveLogoPath(settings?.LogoPath));
                viewBytes = PdfReportService.AppendToPdf(document.Content, controlSheet);
            }

            var wmCtx = new WatermarkContext(
                WatermarkKind.Document, document.Status,
                "Document", document.Id.ToString(), document.Version.ToString("0.00"),
                userName ?? "Unknown", DateTime.UtcNow,
                DocNumber: document.DocumentId,
                DocTypeCode: document.DocType.HasValue ? GetDocTypeShortCode(document.DocType.Value) : null,
                DocTitle: document.Title);
            viewBytes = WatermarkService.Apply(viewBytes, document.FileExtension, wmCtx);

            // Return inline so the browser opens the file rather than downloading it
            return File(viewBytes, document.FileType ?? "application/octet-stream");
        }

        // POST: Documents/Details/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, AppDocument.DocumentStatus status, string InputEmail, string InputPassword, string? RejectionReason = null)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            var documentOperation = (status == AppDocument.DocumentStatus.Approved)
                                                        ? DocumentOperations.Approve
                                                        : DocumentOperations.Reject;
            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, documentOperation);
            if (!isAuthorized.Succeeded)
            {
                return Forbid();
            }
            if (documentOperation == DocumentOperations.Approve)
            {
                if (document.Status != AppDocument.DocumentStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only documents under review can be approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var userSigning = await _userManager.FindByEmailAsync(InputEmail);
                var loggedUser = _userManager.GetUserName(User);
                if (userSigning == null || userSigning.UserName != loggedUser)
                {
                    TempData["WorkflowError"] = "Invalid email or password.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var result = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, InputPassword);
                if (result != PasswordVerificationResult.Success)
                {
                    TempData["WorkflowError"] = "Invalid email or password.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                document.Version = Math.Ceiling(document.Version);
                document.ApprovedBy = _userManager.GetUserName(User);
                document.ApprovedOn = DateTime.UtcNow;
                document.Status = AppDocument.DocumentStatus.Approved;

                if (document.FileExtension != null && document.FileExtension.Equals(".pdf") && document.Content != null)
                {
                    document.Content  = Helper.SignPDF(document.Content, _configuration["CertPassword"]);
                    document.FileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(document.Content);
                }

                if (!string.IsNullOrEmpty(document.GeneratedFrom))
                {
                    var oldDocument = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id.ToString().Equals(document.GeneratedFrom));
                    if (oldDocument != null)
                    {
                        oldDocument.IsLocked = false;
                        oldDocument.Status = AppDocument.DocumentStatus.Obsolete;
                        _context.AppDocument.Update(oldDocument);
                    }
                }

                _context.AppDocument.Update(document);
                _context.DocumentAuditLog.Add(new DocumentAuditLog
                {
                    AppDocumentId = document.Id,
                    Action = "Approved",
                    FromStatus = "UnderReview",
                    ToStatus = "Approved",
                    PerformedBy = _userManager.GetUserName(User),
                    PerformedOn = DateTime.UtcNow
                });
            }
            else
            {
                if (document.Status != AppDocument.DocumentStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only documents under review can be rejected.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (string.IsNullOrWhiteSpace(RejectionReason))
                {
                    TempData["WorkflowError"] = "A rejection reason is required.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var rejectSigning = await _userManager.FindByEmailAsync(InputEmail);
                var rejectLoggedUser = _userManager.GetUserName(User);
                if (rejectSigning == null || rejectSigning.UserName != rejectLoggedUser)
                {
                    TempData["WorkflowError"] = "Invalid email or password.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                var rejectResult = _passwordHasher.VerifyHashedPassword(rejectSigning, rejectSigning.PasswordHash ?? string.Empty, InputPassword);
                if (rejectResult != PasswordVerificationResult.Success)
                {
                    TempData["WorkflowError"] = "Invalid email or password.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var fromStatus = document.Status.ToString();
                document.Status = status;
                _context.AppDocument.Update(document);
                _context.DocumentAuditLog.Add(new DocumentAuditLog
                {
                    AppDocumentId = document.Id,
                    Action = "Rejected",
                    FromStatus = fromStatus,
                    ToStatus = status.ToString(),
                    PerformedBy = _userManager.GetUserName(User),
                    PerformedOn = DateTime.UtcNow,
                    Notes = RejectionReason
                });

                // Restore the original approved document if this was a new-version draft
                if (!string.IsNullOrEmpty(document.GeneratedFrom))
                {
                    var originalDoc = await _context.AppDocument.FirstOrDefaultAsync(
                        m => m.Id.ToString() == document.GeneratedFrom
                          && m.Status == AppDocument.DocumentStatus.Approved
                          && m.IsLocked);
                    if (originalDoc != null)
                    {
                        originalDoc.IsLocked = false;
                        _context.AppDocument.Update(originalDoc);
                        _context.DocumentAuditLog.Add(new DocumentAuditLog
                        {
                            AppDocumentId = originalDoc.Id,
                            Action        = "Restored",
                            FromStatus    = "Approved",
                            ToStatus      = "Approved",
                            PerformedBy   = _userManager.GetUserName(User) ?? "System",
                            PerformedOn   = DateTime.UtcNow,
                            Notes         = $"Unlocked: revision v{document.Version:0.00} was rejected."
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            if (status == AppDocument.DocumentStatus.Approved)
            {
                TempData["Success"] = "Document approved successfully.";
                if (document.DocType.HasValue && _trainingRelevantTypes.Contains(document.DocType.Value))
                    TempData["TrainingReminder"] = $"Training required: this document type ({document.DocType}) typically requires personnel training. Please schedule training records for all affected staff.";
            }
            else
            {
                TempData["Success"] = "Document rejected.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Documents/ObsoleteDocument/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObsoleteDocument(int id, string InputEmail, string InputPassword, string? ObsoleteReason = null)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Approve);
            if (!isAuthorized.Succeeded) return Forbid();

            var allowedStatuses = new[] { AppDocument.DocumentStatus.Effective, AppDocument.DocumentStatus.Approved };
            if (!allowedStatuses.Contains(document.Status) || document.IsLocked)
            {
                TempData["WorkflowError"] = "Only an active (unlocked) Effective or Approved document can be obsoleted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userSigning = await _userManager.FindByEmailAsync(InputEmail);
            var loggedUser = _userManager.GetUserName(User);
            if (userSigning == null || userSigning.UserName != loggedUser)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, InputPassword);
            if (result != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fromStatus = document.Status.ToString();

            // Cancel any in-progress training linked to this document
            var linkedTrainings = await _context.DocumentTrainingRequirement
                .Where(r => r.DocumentId == id)
                .Include(r => r.Training)
                    .ThenInclude(t => t.Trainees)
                .ToListAsync();
            foreach (var req in linkedTrainings)
            {
                if (req.Training == null
                    || req.Training.Status == Training.TrainingStatus.Completed
                    || req.Training.Status == Training.TrainingStatus.Cancelled)
                    continue;

                req.Training.Status = Training.TrainingStatus.Cancelled;
                _context.Training.Update(req.Training);

                foreach (var ut in req.Training.Trainees)
                {
                    if (ut.Status != UserTrainingStatus.Completed && ut.Status != UserTrainingStatus.Failed)
                    {
                        ut.Status = UserTrainingStatus.Cancelled;
                        _context.UserTraining.Update(ut);
                    }
                }
            }

            document.Status = AppDocument.DocumentStatus.Obsolete;
            document.IsLocked = true;
            document.ObsoletedOn = DateTime.UtcNow;
            var _docSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            document.RetainUntil = document.ObsoletedOn.Value.AddYears(_docSets.DocumentRetentionYears);
            _context.AppDocument.Update(document);
            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action = "Obsoleted",
                FromStatus = fromStatus,
                ToStatus = "Obsolete",
                PerformedBy = _userManager.GetUserName(User),
                PerformedOn = DateTime.UtcNow,
                Notes = ObsoleteReason
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document has been marked as Obsolete.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Documents/ReassignAuthor/5
        // Transfers ownership of an in-progress draft to another user — e.g. when the original
        // author leaves the company or can no longer work on the document. Controlled, signed and logged.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> ReassignAuthor(int id, string? NewAuthorId, string? ReassignReason,
                                                         string InputEmail, string InputPassword)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            // Authorship of an approved/effective version is part of the controlled record and must not change;
            // only an in-progress (unlocked) Draft or Under Review document can be reassigned.
            var allowed = new[] { AppDocument.DocumentStatus.Draft, AppDocument.DocumentStatus.UnderReview };
            if (!allowed.Contains(document.Status) || document.IsLocked)
            {
                TempData["WorkflowError"] = "Only an in-progress (Draft or Under Review) document can have its author reassigned.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(ReassignReason))
            {
                TempData["WorkflowError"] = "A reason is required to reassign the author.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Electronic signature — the signer must be the logged-in user.
            var userSigning = await _userManager.FindByEmailAsync(InputEmail ?? string.Empty);
            var loggedUser = _userManager.GetUserName(User);
            if (userSigning == null || userSigning.UserName != loggedUser)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, InputPassword ?? string.Empty);
            if (result != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Resolve and validate the new author.
            var newAuthor = string.IsNullOrWhiteSpace(NewAuthorId) ? null : await _userManager.FindByIdAsync(NewAuthorId);
            if (newAuthor == null)
            {
                TempData["WorkflowError"] = "Please select a valid new author.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (await _userManager.IsInRoleAsync(newAuthor, Constants.ReadOnlyRole))
            {
                TempData["WorkflowError"] = "A read-only user cannot be made the author of a document.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (newAuthor.LockoutEnd.HasValue && newAuthor.LockoutEnd > DateTimeOffset.UtcNow)
            {
                TempData["WorkflowError"] = "The selected user account is inactive.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var previousAuthor = document.AuthoredBy;
            var newAuthorName = newAuthor.UserName ?? string.Empty;
            if (newAuthorName == previousAuthor)
            {
                TempData["WorkflowError"] = "The selected user is already the author of this document.";
                return RedirectToAction(nameof(Details), new { id });
            }

            document.AuthoredBy = newAuthorName;
            _context.AppDocument.Update(document);
            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action = "Author Reassigned",
                FromStatus = null,
                ToStatus = null,
                PerformedBy = loggedUser,
                PerformedOn = DateTime.UtcNow,
                Notes = $"Author reassigned from '{previousAuthor}' to '{newAuthorName}'. Reason: {ReassignReason.Trim()}"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Document author reassigned to {newAuthor.FirstName} {newAuthor.LastName}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Documents/InitiatePeriodicReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitiatePeriodicReview(int id, string InputEmail, string InputPassword)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            // The author, or a document manager (Quality Manager / Authorised Person / Administrator), per the
            // Document Control SOP. The initiator becomes the author of the new version (Reassign author can hand it on).
            var isAuthorized = (await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Update)).Succeeded
                            || (await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Approve)).Succeeded;
            if (!isAuthorized) return Forbid();

            if (document.Status != AppDocument.DocumentStatus.Effective || document.IsLocked)
            {
                TempData["WorkflowError"] = "Only an active Effective document can be sent for periodic review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!document.PeriodicReviewRequired)
            {
                TempData["WorkflowError"] = "This document does not have periodic review enabled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // High #6: enforce overdue condition in controller — view-only guard is insufficient
            if (!document.ReviewDueDate.HasValue || document.ReviewDueDate.Value.Date >= SiteTime.Today)
            {
                TempData["WorkflowError"] = "Periodic review can only be initiated when the review date is overdue.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // High #5: electronic signature required
            var userSigning = await _userManager.FindByEmailAsync(InputEmail);
            var loggedUser = _userManager.GetUserName(User);
            if (userSigning == null || userSigning.UserName != loggedUser)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var sigResult = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, InputPassword);
            if (sigResult != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var newVersion = document.Version + 0.01m;
            var newDoc = new AppDocument
            {
                DocumentId      = document.DocumentId,
                Title           = document.Title,
                Description     = document.Description,
                Version         = newVersion,
                Content         = document.Content,
                FileType        = document.FileType,
                FileExtension   = document.FileExtension,
                FileHash        = document.FileHash,
                AuthoredBy      = _userManager.GetUserName(User),
                AuthoredOn      = DateTime.UtcNow,
                GeneratedFrom   = id.ToString(),
                IsLocked        = false,
                Status          = AppDocument.DocumentStatus.Draft,
                Category        = document.Category,
                DocType         = document.DocType,
                ReasonForChange          = "Periodic Review",
                PeriodicReviewRequired   = document.PeriodicReviewRequired,
                TrainingRequired         = document.TrainingRequired,
                IsExternal           = document.IsExternal,
                ExternalSource       = document.ExternalSource,
                ExternalVersion      = document.ExternalVersion,
                ExternalAuthorName   = document.ExternalAuthorName,
                ExternalAuthoredOn   = document.ExternalAuthoredOn,
                ExternalApproverName = document.ExternalApproverName,
                ExternalApprovedOn   = document.ExternalApprovedOn
            };
            _context.Add(newDoc);
            document.IsLocked = true;
            _context.Update(document);
            // Save here to materialise newDoc.Id before it is referenced in the audit log entries below.
            await _context.SaveChangesAsync();

            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = id,
                Action        = "Periodic Review Initiated",
                FromStatus    = "Effective",
                ToStatus      = "Effective",
                PerformedBy   = _userManager.GetUserName(User),
                PerformedOn   = DateTime.UtcNow,
                Notes         = $"Periodic review initiated. Draft v{newVersion:0.00} created."
            });
            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = newDoc.Id,
                Action        = "New version drafted",
                FromStatus    = "Effective",
                ToStatus      = "Draft",
                PerformedBy   = _userManager.GetUserName(User),
                PerformedOn   = DateTime.UtcNow,
                Notes         = $"Based on v{document.Version:0.00} — Periodic Review"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Periodic review initiated. Draft v{newVersion:0.00} created.";
            return RedirectToAction(nameof(Details), new { id = newDoc.Id });
        }

        // POST: Documents/SubmitForReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(int id, string InputEmail, string InputPassword)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Create);
            if (!isAuthorized.Succeeded) return Forbid();

            if (document.Status != AppDocument.DocumentStatus.Draft)
            {
                TempData["WorkflowError"] = "Only Draft documents can be submitted for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userSigning = await _userManager.FindByEmailAsync(InputEmail);
            var loggedUser = _userManager.GetUserName(User);
            if (userSigning == null || userSigning.UserName != loggedUser)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, InputPassword);
            if (result != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            document.Status = AppDocument.DocumentStatus.UnderReview;
            _context.AppDocument.Update(document);
            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action        = "Submitted for Review",
                FromStatus    = "Draft",
                ToStatus      = "UnderReview",
                PerformedBy   = _userManager.GetUserName(User),
                PerformedOn   = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Document submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Documents/ReturnForRework/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnForRework(int id, string ReturnReason, string InputEmail, string InputPassword)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Reject);
            if (!isAuthorized.Succeeded) return Forbid();

            if (document.Status != AppDocument.DocumentStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only documents under review can be returned for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(ReturnReason))
            {
                TempData["WorkflowError"] = "A reason for return is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // High #4: electronic signature required
            var userSigning = await _userManager.FindByEmailAsync(InputEmail);
            var loggedUser = _userManager.GetUserName(User);
            if (userSigning == null || userSigning.UserName != loggedUser)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var sigResult = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, InputPassword);
            if (sigResult != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            document.Status = AppDocument.DocumentStatus.Draft;
            _context.AppDocument.Update(document);
            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action        = "Returned for Rework",
                FromStatus    = "UnderReview",
                ToStatus      = "Draft",
                PerformedBy   = _userManager.GetUserName(User),
                PerformedOn   = DateTime.UtcNow,
                Notes         = ReturnReason
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document returned for rework. The author can now edit and resubmit.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: AppDocuments/Approve/5
        [HttpGet]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Approve(int id)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            if (document.Status != AppDocument.DocumentStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only documents under review can be approved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Approve);
            if (!isAuthorized.Succeeded) return Forbid();

            await PopulateApproveViewData(document);
            var vm = new ApproveDocumentViewModel
            {
                DocumentId = id,
                TrainingGroups = new List<TrainingGroupViewModel>
                {
                    new TrainingGroupViewModel { Name = $"Training — {document.Title} v{Math.Ceiling(document.Version):0.0}" }
                }
            };
            return View(vm);
        }

        // POST: AppDocuments/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> Approve(int id, ApproveDocumentViewModel vm)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Approve);
            if (!isAuthorized.Succeeded) return Forbid();

            if (document.Status != AppDocument.DocumentStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only documents under review can be approved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Verify e-sig
            var userSigning = await _userManager.FindByEmailAsync(vm.InputEmail);
            var loggedUser = _userManager.GetUserName(User);
            if (userSigning == null || userSigning.UserName != loggedUser)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                await PopulateApproveViewData(document);
                return View(vm);
            }
            var sigResult = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, vm.InputPassword);
            if (sigResult != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                await PopulateApproveViewData(document);
                return View(vm);
            }

            // Validate periodic review date if required
            if (document.PeriodicReviewRequired && !vm.ReviewDueDate.HasValue)
            {
                TempData["WorkflowError"] = "A Next Review Date is required because this document is marked for periodic review.";
                await PopulateApproveViewData(document);
                return View(vm);
            }

            // Validate training groups if training is required
            if (document.TrainingRequired && (vm.TrainingGroups == null || vm.TrainingGroups.Count == 0))
            {
                TempData["WorkflowError"] = "At least one training group is required when Training Required is enabled.";
                await PopulateApproveViewData(document);
                return View(vm);
            }

            // Validate each training group has at least one resolvable trainee
            if (document.TrainingRequired && vm.TrainingGroups != null)
            {
                foreach (var group in vm.TrainingGroups)
                {
                    if (string.IsNullOrWhiteSpace(group.Name)) continue;

                    bool hasTrainees = group.SelectedUserIds?.Count > 0;
                    if (!hasTrainees)
                    {
                        foreach (var role in group.SelectedRoles ?? new List<string>())
                        {
                            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                            if (usersInRole.Count > 0) { hasTrainees = true; break; }
                        }
                    }

                    if (!hasTrainees)
                    {
                        TempData["WorkflowError"] = $"Training group '{group.Name}' has no trainees assigned. Select at least one user or a role that has members, or remove the group.";
                        await PopulateApproveViewData(document);
                        return View(vm);
                    }
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Set document to Approved
                document.Version      = Math.Ceiling(document.Version);
                document.ApprovedBy   = _userManager.GetUserName(User);
                document.ApprovedOn   = DateTime.UtcNow;
                document.Status       = AppDocument.DocumentStatus.Approved;
                document.ReviewDueDate = document.PeriodicReviewRequired ? vm.ReviewDueDate : null;

                if (document.FileExtension != null && document.FileExtension.Equals(".pdf") && document.Content != null)
                {
                    document.Content  = Helper.SignPDF(document.Content, _configuration["CertPassword"]);
                    document.FileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(document.Content);
                }

                // Obsolete predecessor document if this is a revision
                if (!string.IsNullOrEmpty(document.GeneratedFrom))
                {
                    var oldDocument = await _context.AppDocument
                        .FirstOrDefaultAsync(m => m.Id.ToString() == document.GeneratedFrom);
                    if (oldDocument != null)
                    {
                        oldDocument.IsLocked = false;
                        oldDocument.Status   = AppDocument.DocumentStatus.Obsolete;
                        _context.AppDocument.Update(oldDocument);
                    }
                }

                _context.AppDocument.Update(document);
                _context.DocumentAuditLog.Add(new DocumentAuditLog
                {
                    AppDocumentId = document.Id,
                    Action        = "Approved",
                    FromStatus    = "UnderReview",
                    ToStatus      = "Approved",
                    PerformedBy   = _userManager.GetUserName(User),
                    PerformedOn   = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                bool hasAnyTrainees = false;

                if (document.TrainingRequired && vm.TrainingGroups != null)
                {
                    // Generate next TrainingId base
                    var allTrainingIds = await _context.Training.Select(t => t.TrainingId).ToListAsync();
                    int maxTrainNum = allTrainingIds
                        .Select(s => { var p = s?.Split('-'); return p?.Length == 2 && int.TryParse(p[1], out var n) ? n : 0; })
                        .DefaultIfEmpty(0).Max();

                    foreach (var group in vm.TrainingGroups)
                    {
                        if (string.IsNullOrWhiteSpace(group.Name)) continue;

                        maxTrainNum++;
                        var trainingId = $"TRN-{maxTrainNum.ToString().PadLeft(4, '0')}";

                        // Resolve trainer
                        string? trainerEmail = null;
                        if (!string.IsNullOrEmpty(group.TrainerId))
                        {
                            var trainer = await _userManager.FindByIdAsync(group.TrainerId);
                            trainerEmail = trainer?.Email;
                        }

                        // Resolve trainees: union of role-based + individually selected (deduplicated within group)
                        var traineeIds = new HashSet<int>();
                        foreach (var role in group.SelectedRoles ?? new List<string>())
                        {
                            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                            foreach (var u in usersInRole) traineeIds.Add(u.Id);
                        }
                        foreach (var uid in group.SelectedUserIds ?? new List<int>())
                            traineeIds.Add(uid);

                        var training = new Training
                        {
                            TrainingId            = trainingId,
                            Name                  = group.Name,
                            Description           = group.Description ?? string.Empty,
                            CreatedOn             = DateTime.UtcNow,
                            PolicyId              = document.Id,
                            PolicyTitle           = document.Title,
                            TrainerId             = group.TrainerId,
                            TrainerEmail          = trainerEmail,
                            TrainingType          = group.TrainingType,
                            DueDate               = group.DueDate,
                            SourceDocumentVersion = document.Version,
                            Status                = traineeIds.Count == 0
                                                    ? Training.TrainingStatus.Completed
                                                    : Training.TrainingStatus.Scheduled,
                            Trainees              = new List<UserTraining>()
                        };

                        _context.Training.Add(training);
                        await _context.SaveChangesAsync();

                        foreach (var tid in traineeIds)
                        {
                            training.Trainees.Add(new UserTraining
                            {
                                TraineeId  = tid,
                                TrainingId = training.Id,
                                Status     = UserTrainingStatus.Scheduled
                            });
                            hasAnyTrainees = true;
                        }

                        _context.DocumentTrainingRequirement.Add(new DocumentTrainingRequirement
                        {
                            DocumentId      = document.Id,
                            DocumentVersion = document.Version,
                            TrainingId      = training.Id,
                            CreatedOn       = DateTime.UtcNow,
                            CreatedBy       = _userManager.GetUserName(User) ?? "System"
                        });

                        await _context.SaveChangesAsync();
                    }
                }

                // If no training required, or all groups resolved to zero trainees → auto-Effective
                if (!document.TrainingRequired || !hasAnyTrainees)
                {
                    await _docWorkflow.ApplyEffectiveTransition(document, _userManager.GetUserName(User), null);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                TempData["Success"] = "Document approved successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Approval failed for document {DocumentId}", id);
                TempData["WorkflowError"] = $"Approval failed: {ex.Message}";
                await PopulateApproveViewData(document);
                return View(vm);
            }
        }

        // POST: AppDocuments/IssueDocument/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> IssueDocument(int id, string InputEmail, string InputPassword, string? Commentary)
        {
            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Approve);
            if (!isAuthorized.Succeeded) return Forbid();

            if (document.Status != AppDocument.DocumentStatus.Approved)
            {
                TempData["WorkflowError"] = "Manual issue is only available for Approved documents.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(Commentary))
            {
                TempData["WorkflowError"] = "A justification commentary is required for manual issue.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userSigning = await _userManager.FindByEmailAsync(InputEmail);
            var loggedUser = _userManager.GetUserName(User);
            if (userSigning == null || userSigning.UserName != loggedUser)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var sigResult = _passwordHasher.VerifyHashedPassword(userSigning, userSigning.PasswordHash ?? string.Empty, InputPassword);
            if (sigResult != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await _docWorkflow.ApplyEffectiveTransition(document, loggedUser, Commentary);
            _context.DocumentAuditLog.Add(new DocumentAuditLog
            {
                AppDocumentId = document.Id,
                Action        = "Document Issued (Manual Override)",
                FromStatus    = "Approved",
                ToStatus      = "Effective",
                PerformedBy   = loggedUser,
                PerformedOn   = DateTime.UtcNow,
                Notes         = Commentary
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document has been manually issued as Effective.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: AppDocuments/Library
        public async Task<IActionResult> Library(string? category, string? docType, string? search)
        {
            var query = _context.AppDocument
                .Where(d => !d.IsDeleted && d.Status == AppDocument.DocumentStatus.Effective)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<AppDocument.DocumentCategory>(category, out var cat))
                query = query.Where(d => d.Category == cat);

            if (!string.IsNullOrWhiteSpace(docType) && Enum.TryParse<AppDocument.DocumentType>(docType, out var dt))
                query = query.Where(d => d.DocType == dt);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(d => d.Title.Contains(search) || d.Description.Contains(search) || d.DocumentId.Contains(search));
            }

            var docs = await query.OrderBy(d => d.DocumentId).ToListAsync();

            // Populate the filter dropdowns with the keys the Library view reads
            // (ViewData["Categories"] / ViewData["DocTypes"] as List<SelectListItem>).
            ViewData["Categories"] = Enum.GetValues(typeof(AppDocument.DocumentCategory))
                .Cast<AppDocument.DocumentCategory>()
                .Select(c => new SelectListItem { Value = c.ToString(), Text = GetCategoryDisplayName(c) })
                .ToList();
            ViewData["DocTypes"] = Enum.GetValues(typeof(AppDocument.DocumentType))
                .Cast<AppDocument.DocumentType>()
                .Select(t => new SelectListItem { Value = t.ToString(), Text = GetDocTypeDisplayName(t) })
                .ToList();

            ViewBag.Search   = search;
            ViewBag.Category = category;
            ViewBag.DocType  = docType;
            return View(docs);
        }

        // Helper: apply Effective status transition

        private async Task PopulateApproveViewData(AppDocument document)
        {
            var allUsers = _userManager.Users.OrderBy(u => u.UserName).ToList();
            var trainerUsers = (await _userManager.GetUsersInRoleAsync(Constants.TrainerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.QualityManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.AdministratorsRole))
                .DistinctBy(u => u.Id)
                .OrderBy(u => u.UserName)
                .ToList();
            var roles = _roleManager.Roles.OrderBy(r => r.Name).ToList();
            var roleCounts = new Dictionary<string, int>();
            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
                roleCounts[role.Name!] = usersInRole.Count;
            }
            ViewBag.Document       = document;
            ViewBag.AllRoles       = roles;
            ViewBag.AllUsers       = allUsers;
            ViewBag.TrainerUsers   = trainerUsers;
            ViewBag.RoleUserCounts = roleCounts;
        }

        //private static void OpenAndAddToWordprocessingStream(Stream stream, string txt, string strDoc)
        //{
        //    // Open a WordProcessingDocument based on a stream.
        //    WordprocessingDocument wordprocessingDocument = WordprocessingDocument.Open(stream, true);
        //    // Assign a reference to the existing document body.
        //    MainDocumentPart mainDocumentPart = wordprocessingDocument.MainDocumentPart;
        //    FooterPart footerPart = mainDocumentPart.FooterParts.Last();
        //    // Add new text.
        //    Paragraph paragraph1 = new Paragraph() { };
        //    Run run1 = new Run();
        //    Text text1 = new Text();
        //    text1.Text = txt;

        //    run1.Append(text1);
        //    paragraph1.Append(run1);
        //    footerPart.Footer.Append(paragraph1);

        //    wordprocessingDocument.SaveAs(strDoc + ".docx").Dispose();

        //    // Close the document handle.
        //    wordprocessingDocument.Close();

        //    // Caller must close the stream.
        //}

        //private static void ConvertWordToSpecifiedFormat(object input, object output, object format)
        //{
        //    Word._Application application = new Word.Application();
        //    application.Visible = false;
        //    object missing = Missing.Value;
        //    object isVisible = false;
        //    object readOnly = false;
        //    Word._Document document = application.Documents.Open(ref input, ref missing, ref readOnly, ref missing, ref missing, ref missing, ref missing,
        //                            ref missing, ref missing, ref missing, ref missing, ref isVisible, ref missing, ref missing, ref missing, ref missing);
        //    document.Activate();
        //    document.SaveAs(ref output, ref format, ref missing, ref missing, ref missing, ref missing, ref missing, ref missing,
        //                    ref missing, ref missing, ref missing, ref missing, ref missing, ref missing, ref missing, ref missing);
        //    document.Save();
        //    application.Quit(ref missing, ref missing, ref missing);
        //}

        //private static async void WaitForFile(string doc)
        //{
        //    //This will lock the execution until the file is ready
        //    //TODO: Add some logic to make it async and cancelable
        //    while (!IsFileReady(doc)) 
        //    {
        //        Thread.Sleep(1000);
        //    }
        //}

        //private static bool IsFileReady(string doc)
        //{
        //    // If the file can be opened for exclusive access it means that the file
        //    // is no longer locked by another process.
        //    try
        //    {
        //        using (FileStream file = new FileStream(doc, FileMode.Open, FileAccess.Read))
        //            return file.Length > 0;
        //    }
        //    catch (Exception)
        //    {
        //        return false;
        //    }
        //}

        //private static void OpenAndSaveToWordprocessingStream(string output, AppDocument documentPdf)
        //{
        //    using (MemoryStream ms = new MemoryStream())
        //    using (FileStream file = new FileStream(output, FileMode.Open, FileAccess.Read))
        //    {
        //        byte[] bytes = new byte[file.Length];
        //        file.Read(bytes, 0, (int)file.Length);
        //        ms.Write(bytes, 0, (int)file.Length);
        //        documentPdf.Content = ms.ToArray();
        //        documentPdf.FileType = "application/pdf";
        //        documentPdf.FileExtension = ".pdf";
        //    }
        //}

        // GET: Documents/Create
        public IActionResult Create()
        {
            PopulateViewData();
            return View();
        }

        // POST: Documents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,DocumentId,Description,Version,Content,FileType,FileExtension,AuthoredBy,AuthoredOn,ApprovedBy,ApprovedOn,Category,DocType,PeriodicReviewRequired,TrainingRequired,IsExternal,ExternalSource,ExternalVersion,ExternalAuthorName,ExternalAuthoredOn,ExternalApproverName,ExternalApprovedOn")] AppDocument document, IFormFile file)
        {
            // These fields are set by the server — remove them from model state so they don't
            // trigger implicit [Required] validation failures (Nullable=enable project setting)
            ModelState.Remove("DocumentId");
            ModelState.Remove("AuthoredBy");
            ModelState.Remove("AuthoredOn");
            ModelState.Remove("FileType");
            ModelState.Remove("FileExtension");
            ModelState.Remove("FileHash");
            ModelState.Remove("IsLocked");
            ModelState.Remove("ExternalAuthorName");
            ModelState.Remove("ExternalApproverName");

            try
            {
                if (ModelState.IsValid)
                {
                    // Find the highest numeric suffix across all existing DocumentIds (client-side
                    // to avoid string-sort issues with mixed-format IDs like DOC-01 vs DOC-0002)
                    var allDocIds = _context.AppDocument
                        .Where(x => x.DocumentId != null)
                        .Select(x => x.DocumentId)
                        .AsEnumerable();
                    var prevId = allDocIds
                        .Select(id => {
                            var parts = id!.Split('-');
                            return parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    prevId++;
                    document.DocumentId = $"DOC-{prevId.ToString().PadLeft(4, '0')}";
                    document.Version = Convert.ToDecimal(0.01);
                    document.IsLocked = false;
                    document.AuthoredBy = _userManager.GetUserName(User);
                    document.AuthoredOn = DateTime.UtcNow;
                    if (file == null || file.Length == 0)
                    {
                        ModelState.AddModelError("file", "Please select a file to upload.");
                        return View(document);
                    }

                    var allowedExtensions = UploadRules.AllowedExtensions;
                    var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExt))
                    {
                        ModelState.AddModelError("file", $"File type '{fileExt}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.");
                        return View(document);
                    }

                    if (file.Length > UploadRules.MaxBytes)
                    {
                        ModelState.AddModelError("file", "File exceeds the 25 MB maximum size limit.");
                        return View(document);
                    }

                    document.FileType = file.ContentType;
                    document.FileExtension = Path.GetExtension(file.FileName);

                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        document.Content = memoryStream.ToArray();
                        document.FileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(document.Content);
                    }

                    var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Create);
                    if (!isAuthorized.Succeeded)
                    {
                        return Forbid();
                    }

                    _context.AppDocument.Add(document);
                    await _context.SaveChangesAsync();

                    _context.DocumentAuditLog.Add(new DocumentAuditLog
                    {
                        AppDocumentId = document.Id,
                        Action = "Created",
                        FromStatus = null,
                        ToStatus = "Draft",
                        PerformedBy = _userManager.GetUserName(User),
                        PerformedOn = DateTime.UtcNow,
                        Notes = $"ID: {document.DocumentId}; Title: {document.Title}; Category: {document.Category}; Authored by: {document.AuthoredBy}"
                    });
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Details), new { id = document.Id });
                }
            }
            catch (DataException /* dex */)
            {
                //Log the error (uncomment dex variable name and add a line here to write a log.
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            PopulateViewData(document.Category);
            return View(document);
        }

        // GET: Documents/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var document = await _context.AppDocument.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Update);
            if (!isAuthorized.Succeeded)
            {
                return Forbid();
            }

            if (document.IsLocked || document.Status == AppDocument.DocumentStatus.Obsolete)
            {
                return Forbid();
            }

            if (document.Status == AppDocument.DocumentStatus.UnderReview)
            {
                TempData["Error"] = "This document is currently under review and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            PopulateViewData(document.Category);
            return View(document);
        }

        // POST: Documents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,DocumentId,Title,Description,Version,Content,FileType,FileExtension,AuthoredBy,AuthoredOn,ApprovedBy,ApprovedOn,GeneratedFrom,IsLocked,Status,Category,DocType,ReasonForChange,PeriodicReviewRequired,TrainingRequired,IsExternal,ExternalSource,ExternalVersion,ExternalAuthorName,ExternalAuthoredOn,ExternalApproverName,ExternalApprovedOn")] AppDocument document, IFormFile? file)
        {
            if (id != document.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Load lock status and current status from DB — do not trust submitted form values
                    var dbDoc = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    if (dbDoc == null) return NotFound();
                    if (dbDoc.IsLocked || dbDoc.Status == AppDocument.DocumentStatus.Obsolete)
                        return Forbid();

                    // Mirror the GET guard: content under review must not change. The route back
                    // to editing is "Return for Rework" (e-signed, with reason), not a silent demotion.
                    if (dbDoc.Status == AppDocument.DocumentStatus.UnderReview)
                    {
                        TempData["Error"] = "This document is currently under review and cannot be edited.";
                        return RedirectToAction(nameof(Details), new { id });
                    }

                    // Preserve DB-controlled fields that must not be overridden by form submission
                    document.IsLocked = dbDoc.IsLocked;
                    document.ObsoletedOn = dbDoc.ObsoletedOn;

                    // Handle file upload (optional on edit — keeps existing file if none selected)
                    if (file != null && file.Length > 0)
                    {
                        var allowedExtensions = UploadRules.AllowedExtensions;
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(ext))
                        {
                            ModelState.AddModelError("file", $"File type '{ext}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.");
                            return View(document);
                        }

                        if (file.Length > UploadRules.MaxBytes)
                        {
                            ModelState.AddModelError("file", "File exceeds the 25 MB maximum size limit.");
                            return View(document);
                        }

                        using (var memoryStream = new MemoryStream())
                        {
                            await file.CopyToAsync(memoryStream);
                            document.Content = memoryStream.ToArray();
                            document.FileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(document.Content);
                        }

                        document.FileType = file.ContentType;
                        document.FileExtension = Path.GetExtension(file.FileName);
                    }
                    else
                    {
                        // Keep existing file content from database
                        var existing = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                        document.Content = existing?.Content;
                        document.FileType = existing?.FileType;
                        document.FileExtension = existing?.FileExtension;
                        document.FileHash = existing?.FileHash;
                    }

                    // Preserve original author — only a new version (Approved → draft) sets a new AuthoredBy/On
                    document.AuthoredBy = dbDoc.AuthoredBy;
                    document.AuthoredOn = dbDoc.AuthoredOn;
                    // Preserve IsExternal flag — cannot be changed after creation
                    document.IsExternal = dbDoc.IsExternal;

                    var isAuthorized = await _authorizationService.AuthorizeAsync(User, document, DocumentOperations.Update);
                    if (!isAuthorized.Succeeded)
                    {
                        return Forbid();
                    }

                    // Field-level change tracking for the audit log (dbDoc = pre-edit snapshot)
                    var diff = new List<string>();
                    OpenQMS.Services.AuditDiffHelper.Track(diff,     "Title",                    dbDoc.Title,                  document.Title);
                    OpenQMS.Services.AuditDiffHelper.Track(diff,     "Description",              dbDoc.Description,            document.Description);
                    OpenQMS.Services.AuditDiffHelper.TrackEnum(diff, "Category",                 dbDoc.Category,               document.Category);
                    OpenQMS.Services.AuditDiffHelper.TrackEnum(diff, "Document Type",            dbDoc.DocType,                document.DocType);
                    OpenQMS.Services.AuditDiffHelper.Track(diff,     "Reason for Change",        dbDoc.ReasonForChange,        document.ReasonForChange);
                    OpenQMS.Services.AuditDiffHelper.Track<bool>(diff, "Training Required",        dbDoc.TrainingRequired,       document.TrainingRequired);
                    OpenQMS.Services.AuditDiffHelper.Track<bool>(diff, "Periodic Review Required", dbDoc.PeriodicReviewRequired, document.PeriodicReviewRequired);
                    OpenQMS.Services.AuditDiffHelper.TrackDate(diff, "Next Review Date",         dbDoc.ReviewDueDate,          document.ReviewDueDate);
                    if (dbDoc.IsExternal)
                    {
                        OpenQMS.Services.AuditDiffHelper.Track(diff,     "External Source",      dbDoc.ExternalSource,       document.ExternalSource);
                        OpenQMS.Services.AuditDiffHelper.Track(diff,     "External Version",     dbDoc.ExternalVersion,      document.ExternalVersion);
                        OpenQMS.Services.AuditDiffHelper.Track(diff,     "External Author",      dbDoc.ExternalAuthorName,   document.ExternalAuthorName);
                        OpenQMS.Services.AuditDiffHelper.TrackDate(diff, "External Authored On", dbDoc.ExternalAuthoredOn,   document.ExternalAuthoredOn);
                        OpenQMS.Services.AuditDiffHelper.Track(diff,     "External Approver",    dbDoc.ExternalApproverName, document.ExternalApproverName);
                        OpenQMS.Services.AuditDiffHelper.TrackDate(diff, "External Approved On", dbDoc.ExternalApprovedOn,   document.ExternalApprovedOn);
                    }
                    if (file != null && file.Length > 0)
                        diff.Add($"File: replaced (\"{file.FileName}\")");
                    var editNotes = OpenQMS.Services.AuditDiffHelper.Build(diff);

                    if (dbDoc.Status == AppDocument.DocumentStatus.Approved
                        || dbDoc.Status == AppDocument.DocumentStatus.Effective)
                    {
                        // Bump minor version for a new draft created from an Approved OR Effective
                        // document. Both are "live" states the user can revise via "New Version";
                        // basing the decision and the bump on the trusted dbDoc (not the posted
                        // form values) prevents an Effective doc being silently demoted to Draft.
                        document.Version = dbDoc.Version + Convert.ToDecimal(0.01);

                        // Create a new draft from the approved document (SQLite-compatible — no IDENTITY_INSERT needed)
                        var newDoc = new AppDocument
                        {
                            DocumentId = document.DocumentId,
                            Title = document.Title,
                            Description = document.Description,
                            Version = document.Version,
                            Content = document.Content,
                            FileType = document.FileType,
                            FileExtension = document.FileExtension,
                            FileHash = document.FileHash,
                            AuthoredBy = _userManager.GetUserName(User),
                            AuthoredOn = DateTime.UtcNow,
                            GeneratedFrom = id.ToString(),
                            IsLocked = false,
                            Status = AppDocument.DocumentStatus.Draft,
                            Category = document.Category,
                            DocType = document.DocType,
                            ReasonForChange = document.ReasonForChange,
                            TrainingRequired         = document.TrainingRequired,
                            PeriodicReviewRequired   = document.PeriodicReviewRequired,
                            ReviewDueDate        = document.ReviewDueDate,
                            IsExternal           = document.IsExternal,
                            ExternalSource       = document.ExternalSource,
                            ExternalVersion      = document.ExternalVersion,
                            ExternalAuthorName   = document.ExternalAuthorName,
                            ExternalAuthoredOn   = document.ExternalAuthoredOn,
                            ExternalApproverName = document.ExternalApproverName,
                            ExternalApprovedOn   = document.ExternalApprovedOn,
                        };
                        _context.Add(newDoc);
                        var oldDoc = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
                        oldDoc.IsLocked = true;
                        _context.Update(oldDoc);
                        await _context.SaveChangesAsync();

                        var notesText = $"Based on v{oldDoc.Version:0.00}";
                        if (!string.IsNullOrWhiteSpace(document.ReasonForChange))
                            notesText += $" — {document.ReasonForChange}";
                        if (diff.Count > 0)
                            notesText += $" | {editNotes}";

                        _context.DocumentAuditLog.Add(new DocumentAuditLog
                        {
                            AppDocumentId = newDoc.Id,
                            Action = "New version drafted",
                            FromStatus = "Approved",
                            ToStatus = "Draft",
                            PerformedBy = document.AuthoredBy,
                            PerformedOn = DateTime.UtcNow,
                            Notes = notesText
                        });
                        await _context.SaveChangesAsync();

                        return RedirectToAction(nameof(Details), new { id = newDoc.Id });
                    }
                    else
                    {
                        // Draft save — version unchanged, only content/metadata updated
                        document.Status = AppDocument.DocumentStatus.Draft;
                        _context.Update(document);
                        await _context.SaveChangesAsync();

                        if (diff.Count > 0)
                        {
                            _context.DocumentAuditLog.Add(new DocumentAuditLog
                            {
                                AppDocumentId = document.Id,
                                Action = "Updated",
                                FromStatus = "Draft",
                                ToStatus = "Draft",
                                PerformedBy = document.AuthoredBy,
                                PerformedOn = DateTime.UtcNow,
                                Notes = editNotes
                            });
                            await _context.SaveChangesAsync();
                        }

                        return RedirectToAction(nameof(Details), new { id = document.Id });
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DocumentExists(document.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            PopulateViewData(document.Category);
            return View(document);
        }

        // GET: Documents/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            var document = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == id);
            if (document == null) return NotFound();

            if (document.Status == AppDocument.DocumentStatus.Approved
                || document.Status == AppDocument.DocumentStatus.Effective
                || document.Status == AppDocument.DocumentStatus.Obsolete
                || document.Status == AppDocument.DocumentStatus.UnderReview
                || document.Status == AppDocument.DocumentStatus.Rejected)
            {
                TempData["Error"] = $"Documents with status '{EnumHelper.Display(document.Status)}' cannot be deleted. Use the Obsolete workflow to retire active documents.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(document);
        }

        // POST: Documents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!User.IsInRole(Constants.AdministratorsRole))
                return Forbid();

            var document = await _context.AppDocument.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Guard matches the GET handler — Effective, Approved, Obsolete, UnderReview, Rejected cannot be deleted
            if (document.Status != AppDocument.DocumentStatus.Approved
                && document.Status != AppDocument.DocumentStatus.Effective
                && document.Status != AppDocument.DocumentStatus.Obsolete
                && document.Status != AppDocument.DocumentStatus.Rejected
                && document.Status != AppDocument.DocumentStatus.UnderReview)
            {
                _context.DocumentAuditLog.Add(new DocumentAuditLog
                {
                    AppDocumentId = document.Id,
                    Action = "Deleted",
                    FromStatus = document.Status.ToString(),
                    ToStatus = null,
                    PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                    PerformedOn = DateTime.UtcNow
                });
                document.IsDeleted = true;
                document.DeletedBy = _userManager.GetUserName(User);
                document.DeletedOn = DateTime.UtcNow;
                _context.AppDocument.Update(document);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return Forbid();
            }
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            var document = _context.AppDocument.FirstOrDefault(x => x.Id == id);
            if (document == null)
                return NotFound();

            // Medium #8: same access control as Details GET and DownloadFile
            var csvAuthorized = User.IsInRole(Constants.QualityManagerRole)
                             || User.IsInRole(Constants.AdministratorsRole)
                             || User.IsInRole(Constants.AuthorisedPersonRole);
            var csvUser = _userManager.GetUserName(User);
            if (!csvAuthorized
                && csvUser != document.AuthoredBy
                && document.Status != AppDocument.DocumentStatus.Approved
                && document.Status != AppDocument.DocumentStatus.Effective)
                return Forbid();

            var builder = new StringBuilder();
            builder.AppendLine("Document ID,Title,Category,Document Type,Description,Version,Status,File Type,Authored By,Authored On,Approved By,Approved On,Issued On,Reason for Change,Next Review Date");
            builder.AppendLine($"{document.DocumentId},{EscapeCsv(document.Title)},{EscapeCsv(document.Category.HasValue ? GetCategoryDisplayName(document.Category.Value) : string.Empty)},{EscapeCsv(document.DocType.HasValue ? GetDocTypeDisplayName(document.DocType.Value) : string.Empty)},{EscapeCsv(document.Description)},{document.Version:0.00},{document.Status},{document.FileExtension?.TrimStart('.').ToUpperInvariant()},{document.AuthoredBy},{document.AuthoredOn.ToSite():dd/MM/yyyy},{document.ApprovedBy ?? string.Empty},{(document.ApprovedOn.HasValue ? document.ApprovedOn.Value.ToSite().ToString("dd/MM/yyyy") : string.Empty)},{(document.IssuedOn.HasValue ? document.IssuedOn.Value.ToSite().ToString("dd/MM/yyyy") : string.Empty)},{EscapeCsv(document.ReasonForChange)},{(document.ReviewDueDate.HasValue ? document.ReviewDueDate.Value.ToString("dd/MM/yyyy") : string.Empty)}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"{document.DocumentId}.csv");
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        // GET: AppDocuments/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            if (!User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var all = await _context.AppDocument.Where(d => !d.IsDeleted).ToListAsync();

            var statusCounts = Enum.GetValues(typeof(AppDocument.DocumentStatus))
                .Cast<AppDocument.DocumentStatus>()
                .ToDictionary(s => s, s => all.Count(d => d.Status == s));

            ViewBag.TotalDocuments   = all.Count(d => d.Status != AppDocument.DocumentStatus.Obsolete);
            ViewBag.EffectiveCount   = statusCounts.GetValueOrDefault(AppDocument.DocumentStatus.Effective);
            ViewBag.DraftCount       = statusCounts.GetValueOrDefault(AppDocument.DocumentStatus.Draft);
            ViewBag.ApprovedCount    = statusCounts.GetValueOrDefault(AppDocument.DocumentStatus.Approved);
            ViewBag.RejectedCount    = statusCounts.GetValueOrDefault(AppDocument.DocumentStatus.Rejected);
            ViewBag.ObsoleteCount    = statusCounts.GetValueOrDefault(AppDocument.DocumentStatus.Obsolete);
            ViewBag.UnderReviewCount = statusCounts.GetValueOrDefault(AppDocument.DocumentStatus.UnderReview);
            ViewBag.StatusCounts     = statusCounts;

            ViewBag.PendingApproval = all
                .Where(d => d.Status == AppDocument.DocumentStatus.UnderReview && !d.IsLocked)
                .OrderBy(d => d.AuthoredOn)
                .Take(10)
                .ToList();

            ViewBag.RecentlyApproved = all
                .Where(d => d.Status == AppDocument.DocumentStatus.Approved)
                .OrderByDescending(d => d.ApprovedOn)
                .Take(10)
                .ToList();

            var recentLogs = await _context.DocumentAuditLog
                .OrderByDescending(l => l.PerformedOn)
                .Take(12)
                .ToListAsync();

            var docIds = recentLogs.Select(l => l.AppDocumentId).Distinct().ToList();
            var recentDocMap = await _context.AppDocument
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.DocumentId);

            ViewBag.RecentActivity = recentLogs;
            ViewBag.RecentDocMap   = recentDocMap;

            var today = SiteTime.Today;
            ViewBag.ReviewOverdue = all
                .Where(d => d.ReviewDueDate.HasValue && d.ReviewDueDate.Value.Date < today
                         && d.Status == AppDocument.DocumentStatus.Effective && !d.IsLocked)
                .OrderBy(d => d.ReviewDueDate)
                .ToList();
            ViewBag.ReviewDueSoon = all
                .Where(d => d.ReviewDueDate.HasValue
                         && d.ReviewDueDate.Value.Date >= today
                         && d.ReviewDueDate.Value.Date <= today.AddDays(30)
                         && d.Status == AppDocument.DocumentStatus.Effective && !d.IsLocked)
                .OrderBy(d => d.ReviewDueDate)
                .ToList();

            return View();
        }

        // GET: AppDocuments/ReviewRegister
        public async Task<IActionResult> ReviewRegister()
        {
            if (!User.IsInRole(Constants.QualityManagerRole)
                && !User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var today = SiteTime.Today;
            var docs = await _context.AppDocument
                .Where(d => !d.IsDeleted
                         && d.PeriodicReviewRequired
                         && d.ReviewDueDate.HasValue
                         && d.Status == AppDocument.DocumentStatus.Effective
                         && !d.IsLocked)
                .OrderBy(d => d.ReviewDueDate)
                .ToListAsync();

            ViewBag.Today        = today;
            ViewBag.OverdueCount = docs.Count(d => d.ReviewDueDate!.Value.Date < today);
            ViewBag.DueSoonCount = docs.Count(d => d.ReviewDueDate!.Value.Date >= today
                                                 && d.ReviewDueDate!.Value.Date <= today.AddDays(30));
            ViewBag.CurrentCount = docs.Count(d => d.ReviewDueDate!.Value.Date > today.AddDays(30));
            return View(docs);
        }

        public async Task<IActionResult> ExportRegister()
        {
            // Medium #8: restrict full register export to managers, admins and authorised persons
            if (!User.IsInRole(Constants.QualityManagerRole) && !User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var documents = await _context.AppDocument
                .Where(d => !d.IsDeleted)
                .OrderBy(d => d.DocumentId)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Documents");

            // Header row
            var headers = new[] {
                "Document ID", "Title", "Category", "Document Type", "Description", "Version", "Status",
                "File Type", "Is Locked", "Generated From",
                "Authored By", "Authored On", "Approved By", "Approved On", "Issued On", "Reason for Change",
                "Next Review Date"
            };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }
            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            ws.Row(1).Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var d in documents)
            {
                ws.Cell(row, 1).Value = d.DocumentId;
                ws.Cell(row, 2).Value = d.Title;
                ws.Cell(row, 3).Value = d.Category.HasValue ? GetCategoryDisplayName(d.Category.Value) : string.Empty;
                ws.Cell(row, 4).Value = d.DocType.HasValue ? GetDocTypeDisplayName(d.DocType.Value) : string.Empty;
                ws.Cell(row, 5).Value = d.Description ?? string.Empty;
                ws.Cell(row, 6).Value = d.Version.ToString("0.00");
                ws.Cell(row, 7).Value = d.Status.ToString();
                ws.Cell(row, 8).Value = d.FileExtension?.TrimStart('.').ToUpperInvariant() ?? string.Empty;
                ws.Cell(row, 9).Value = d.IsLocked ? "Yes" : "No";
                ws.Cell(row, 10).Value = d.GeneratedFrom ?? string.Empty;
                ws.Cell(row, 11).Value = d.AuthoredBy ?? string.Empty;
                ws.Cell(row, 12).Value = d.AuthoredOn.ToSite().ToString("dd/MM/yyyy");
                ws.Cell(row, 13).Value = d.ApprovedBy ?? string.Empty;
                ws.Cell(row, 14).Value = d.ApprovedOn.HasValue ? d.ApprovedOn.Value.ToSite().ToString("dd/MM/yyyy") : string.Empty;
                ws.Cell(row, 15).Value = d.IssuedOn.HasValue ? d.IssuedOn.Value.ToSite().ToString("dd/MM/yyyy") : string.Empty;
                ws.Cell(row, 16).Value = d.ReasonForChange ?? string.Empty;
                ws.Cell(row, 17).Value = d.ReviewDueDate.HasValue ? d.ReviewDueDate.Value.ToString("dd/MM/yyyy") : string.Empty;

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");

                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Documents_Register_{SiteTime.Today:yyyyMMdd}.xlsx");
        }

        public ActionResult GetDocuments()
        {
            if (!User.IsInRole(Constants.QualityManagerRole) && !User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var appDocuments = _context.AppDocument.Where(d => !d.IsDeleted).ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(AppDocument.DocumentStatus)).Cast<AppDocument.DocumentStatus>();
            var labels = new List<string>();
            var dataSet = new PoliciesChartViewModel();

            var statusColors = new Dictionary<AppDocument.DocumentStatus, string>
            {
                [AppDocument.DocumentStatus.Draft]       = "#ffc107",
                [AppDocument.DocumentStatus.Approved]    = "#0d6efd",
                [AppDocument.DocumentStatus.Rejected]    = "#dc3545",
                [AppDocument.DocumentStatus.Obsolete]    = "#6c757d",
                [AppDocument.DocumentStatus.UnderReview] = "#0dcaf0",
                [AppDocument.DocumentStatus.Effective]   = "#198754",
            };

            if (appDocuments != null && appDocuments.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = appDocuments.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(statusColors.TryGetValue(label, out var color) ? color : "#adb5bd");
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public ActionResult GetDocumentsForBarchart()
        {
            if (!User.IsInRole(Constants.QualityManagerRole) && !User.IsInRole(Constants.AdministratorsRole)
                && !User.IsInRole(Constants.QualityUserRole)
                && !User.IsInRole(Constants.AuthorisedPersonRole))
                return Forbid();

            var appDocuments = _context.AppDocument.Where(d => !d.IsDeleted).ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var dataSet = new PoliciesChartViewModel();

            // Last 12 calendar months (current month inclusive)
            var firstMonth = new DateTime(SiteTime.Today.Year, SiteTime.Today.Month, 1).AddMonths(-11);
            for (int i = 0; i < 12; i++)
            {
                var month = firstMonth.AddMonths(i);
                labels.Add(month.ToString("MMM yyyy"));
                var count = appDocuments.Count(d => d.AuthoredOn.Year == month.Year && d.AuthoredOn.Month == month.Month);
                dataSet.Data.Add(count);
                dataSet.BackgroundColor.Add("#0d6efd");
            }

            dataSets.Add(dataSet);
            return Json(new { dataSet = dataSets, labels });
        }

        private bool DocumentExists(int id)
        {
            return _context.AppDocument.Any(e => e.Id == id);
        }

        private void PopulateViewData(AppDocument.DocumentCategory? selectedCategory = null)
        {
            ViewData["CategoryList"] = new SelectList(
                Enum.GetValues(typeof(AppDocument.DocumentCategory))
                    .Cast<AppDocument.DocumentCategory>()
                    .Select(c => new { Value = c.ToString(), Text = GetCategoryDisplayName(c) }),
                "Value", "Text",
                selectedCategory?.ToString());

            // Pre-populate type list for the selected category (for edit/re-show)
            if (selectedCategory.HasValue && _categoryTypeMap.TryGetValue(selectedCategory.Value, out var types))
            {
                ViewData["DocTypeList"] = new SelectList(
                    types.Select(t => new { Value = t.ToString(), Text = GetDocTypeDisplayName(t) }),
                    "Value", "Text");
            }
            else
            {
                ViewData["DocTypeList"] = new SelectList(Enumerable.Empty<object>(), "Value", "Text");
            }

            // System users list for external document provenance datalist
            ViewData["SystemUsers"] = _userManager.Users
                .ToList()
                .Select(u => string.IsNullOrWhiteSpace(u.FirstName + u.LastName)
                    ? u.UserName ?? u.Email
                    : (u.FirstName + " " + u.LastName).Trim())
                .Where(n => n != null)
                .OrderBy(n => n)
                .ToList();
        }

        private static readonly HashSet<AppDocument.DocumentType> _trainingRelevantTypes = new()
        {
            AppDocument.DocumentType.QualityManual,
            AppDocument.DocumentType.Policy,
            AppDocument.DocumentType.StandardOperatingProcedure,
            AppDocument.DocumentType.WorkInstruction,
            AppDocument.DocumentType.Guideline,
            AppDocument.DocumentType.TrainingMaterial,
            AppDocument.DocumentType.CompetencyAssessment
        };

        private static readonly Dictionary<AppDocument.DocumentCategory, AppDocument.DocumentType[]> _categoryTypeMap = new()
        {
            [AppDocument.DocumentCategory.QualitySystem] = new[]
            {
                AppDocument.DocumentType.QualityManual, AppDocument.DocumentType.Policy,
                AppDocument.DocumentType.StandardOperatingProcedure, AppDocument.DocumentType.WorkInstruction,
                AppDocument.DocumentType.Form, AppDocument.DocumentType.Template,
                AppDocument.DocumentType.Guideline, AppDocument.DocumentType.SiteMasterFile,
                AppDocument.DocumentType.ManagementReview, AppDocument.DocumentType.ProductQualityReview,
                AppDocument.DocumentType.RiskAssessment
            },
            [AppDocument.DocumentCategory.Technical] = new[]
            {
                AppDocument.DocumentType.Specification, AppDocument.DocumentType.Protocol,
                AppDocument.DocumentType.ValidationReport, AppDocument.DocumentType.StudyReport,
                AppDocument.DocumentType.TestReport, AppDocument.DocumentType.MasterBatchRecord,
                AppDocument.DocumentType.TestMethod
            },
            [AppDocument.DocumentCategory.Regulatory] = new[]
            {
                AppDocument.DocumentType.RegulatorySubmission, AppDocument.DocumentType.ProductDossier,
                AppDocument.DocumentType.RegulatoryCorrespondence, AppDocument.DocumentType.Labelling
            },
            [AppDocument.DocumentCategory.Agreements] = new[]
            {
                AppDocument.DocumentType.QualityAgreement, AppDocument.DocumentType.SupplyAgreement,
                AppDocument.DocumentType.ConfidentialityAgreement
            },
            [AppDocument.DocumentCategory.Training] = new[]
            {
                AppDocument.DocumentType.TrainingMaterial, AppDocument.DocumentType.CompetencyAssessment
            },
            [AppDocument.DocumentCategory.Records] = new[]
            {
                AppDocument.DocumentType.CertificateOfAnalysis, AppDocument.DocumentType.AuditReport,
                AppDocument.DocumentType.MeetingMinutes
            }
        };

        private static string GetCategoryDisplayName(AppDocument.DocumentCategory cat) => cat switch
        {
            AppDocument.DocumentCategory.QualitySystem => "Quality System",
            AppDocument.DocumentCategory.Technical     => "Technical",
            AppDocument.DocumentCategory.Regulatory    => "Regulatory",
            AppDocument.DocumentCategory.Agreements    => "Agreements",
            AppDocument.DocumentCategory.Training      => "Training",
            AppDocument.DocumentCategory.Records       => "Records",
            _                                          => cat.ToString()
        };

        // Compact code used in the watermark header identity line (kept short so
        // it sits cleanly beside the document number and version).
        private static string GetDocTypeShortCode(AppDocument.DocumentType? type) => type switch
        {
            AppDocument.DocumentType.QualityManual              => "Manual",
            AppDocument.DocumentType.Policy                     => "Policy",
            AppDocument.DocumentType.StandardOperatingProcedure => "SOP",
            AppDocument.DocumentType.WorkInstruction            => "WI",
            AppDocument.DocumentType.Form                       => "Form",
            AppDocument.DocumentType.Template                   => "Template",
            AppDocument.DocumentType.Guideline                  => "Guideline",
            AppDocument.DocumentType.SiteMasterFile             => "SMF",
            AppDocument.DocumentType.ManagementReview           => "Mgmt Review",
            AppDocument.DocumentType.ProductQualityReview       => "PQR",
            AppDocument.DocumentType.RiskAssessment             => "Risk Assmt",
            AppDocument.DocumentType.Specification              => "Spec",
            AppDocument.DocumentType.Protocol                   => "Protocol",
            AppDocument.DocumentType.ValidationReport           => "Val Report",
            AppDocument.DocumentType.StudyReport                => "Study Report",
            AppDocument.DocumentType.TestReport                 => "Test Report",
            AppDocument.DocumentType.MasterBatchRecord          => "MBR",
            AppDocument.DocumentType.TestMethod                 => "Test Method",
            AppDocument.DocumentType.RegulatorySubmission       => "Reg Submission",
            AppDocument.DocumentType.ProductDossier             => "Dossier",
            AppDocument.DocumentType.RegulatoryCorrespondence   => "Reg Corr",
            AppDocument.DocumentType.Labelling                  => "Labelling",
            AppDocument.DocumentType.QualityAgreement           => "Quality Agmt",
            AppDocument.DocumentType.SupplyAgreement            => "Supply Agmt",
            AppDocument.DocumentType.ConfidentialityAgreement   => "CDA",
            AppDocument.DocumentType.TrainingMaterial           => "Training",
            AppDocument.DocumentType.CompetencyAssessment       => "Competency",
            AppDocument.DocumentType.CertificateOfAnalysis      => "CoA",
            AppDocument.DocumentType.AuditReport                => "Audit Report",
            AppDocument.DocumentType.MeetingMinutes             => "Minutes",
            _                                                   => ""
        };

        private static string GetDocTypeDisplayName(AppDocument.DocumentType type) => type switch
        {
            AppDocument.DocumentType.QualityManual                 => "Quality Manual",
            AppDocument.DocumentType.Policy                        => "Policy",
            AppDocument.DocumentType.StandardOperatingProcedure    => "Standard Operating Procedure (SOP)",
            AppDocument.DocumentType.WorkInstruction               => "Work Instruction (WI)",
            AppDocument.DocumentType.Form                          => "Form",
            AppDocument.DocumentType.Template                      => "Template",
            AppDocument.DocumentType.Guideline                     => "Guideline",
            AppDocument.DocumentType.SiteMasterFile                => "Site Master File",
            AppDocument.DocumentType.ManagementReview              => "Management Review",
            AppDocument.DocumentType.ProductQualityReview          => "Product Quality Review (PQR)",
            AppDocument.DocumentType.RiskAssessment                => "Risk Assessment",
            AppDocument.DocumentType.Specification                 => "Specification",
            AppDocument.DocumentType.Protocol                      => "Protocol",
            AppDocument.DocumentType.ValidationReport              => "Validation Report",
            AppDocument.DocumentType.StudyReport                   => "Study Report",
            AppDocument.DocumentType.TestReport                    => "Test Report",
            AppDocument.DocumentType.MasterBatchRecord             => "Master Batch Record",
            AppDocument.DocumentType.TestMethod                    => "Test Method",
            AppDocument.DocumentType.RegulatorySubmission          => "Regulatory Submission",
            AppDocument.DocumentType.ProductDossier                => "Product Dossier",
            AppDocument.DocumentType.RegulatoryCorrespondence      => "Regulatory Correspondence",
            AppDocument.DocumentType.Labelling                     => "Labelling",
            AppDocument.DocumentType.QualityAgreement              => "Quality Agreement",
            AppDocument.DocumentType.SupplyAgreement               => "Supply Agreement",
            AppDocument.DocumentType.ConfidentialityAgreement      => "Confidentiality Agreement",
            AppDocument.DocumentType.TrainingMaterial              => "Training Material",
            AppDocument.DocumentType.CompetencyAssessment          => "Competency Assessment",
            AppDocument.DocumentType.CertificateOfAnalysis         => "Certificate of Analysis (CoA)",
            AppDocument.DocumentType.AuditReport                   => "Audit Report",
            AppDocument.DocumentType.MeetingMinutes                => "Meeting Minutes",
            _                                                      => type.ToString()
        };
    }
}
