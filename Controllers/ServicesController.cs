using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Services;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.ViewModels;
using static OpenQMS.Models.Service;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public ServicesController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        private async Task SyncServiceRelatedDocs(int serviceId, int[] docIds)
        {
            _context.ServiceLinkedDocument.RemoveRange(
                await _context.ServiceLinkedDocument.Where(x => x.ServiceId == serviceId).ToListAsync());
            foreach (var docId in docIds.Distinct())
            {
                var doc = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null)
                    _context.ServiceLinkedDocument.Add(new ServiceLinkedDocument
                        { ServiceId = serviceId, AppDocumentId = docId, LinkedVersion = doc.Version });
            }
        }

        private void PopulateServiceRelatedDocsViewData(int[]? selectedIds = null)
        {
            ViewData["EffectiveDocs"] = _context.AppDocument
                .Where(d => !d.IsDeleted && d.Status == AppDocument.DocumentStatus.Effective)
                .OrderBy(d => d.DocumentId)
                .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text  = $"{d.DocumentId} v{d.Version.ToString("0.00")} — {d.Title}"
                }).ToList();
            ViewBag.SelectedRelatedDocIds = selectedIds?.ToList() ?? new System.Collections.Generic.List<int>();
        }

        // GET: Services
        public async Task<IActionResult> Index()
        {
            return View(await _context.Service.Where(s => !s.IsDeleted).ToListAsync());
        }

        // GET: Services/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Service == null)
                return NotFound();

            var service = await _context.Service
                .Include(s => s.Documents)
                .Include(s => s.RelatedDocuments!).ThenInclude(rd => rd.Document)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (service == null)
                return NotFound();

            ViewBag.AuditLogs = await _context.MasterDataAuditLog
                .Where(l => l.EntityType == "Service" && l.ParentId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            return View(service);
        }

        // POST: Services/Details/5 (Approval)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, string InputEmail, string InputPassword)
        {
            var service = _context.Service.FirstOrDefault(x => x.Id == id);

            try
            {
                var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.AuthorisedPersonRole);
                if (!isAuthorized)
                {
                    TempData["WorkflowError"] = "You do not have permission to perform this action.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var userSigning = await _userManager.FindByEmailAsync(InputEmail);
                var loggedUser = _userManager.GetUserName(User);
                if ((userSigning == null) || (userSigning.UserName != loggedUser))
                {
                    TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var result = await _signInManager.PasswordSignInAsync(userSigning.UserName, InputPassword, isPersistent: false, lockoutOnFailure: false);
                if (!(result.Succeeded || result.RequiresTwoFactor))
                {
                    TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (service.Status != ServiceStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only services under review can be approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                service.Version = Decimal.ToInt32(service.Version) + 1;
                service.ApprovedBy = _userManager.GetUserName(User);
                service.ApprovedOn = DateTime.UtcNow;
                service.Status = ServiceStatus.Approved;
                service.ExportFilePath = String.Empty;

                if (service.GeneratedFrom != null && !string.IsNullOrEmpty(service.GeneratedFrom))
                {
                    var oldService = await _context.Service.FirstOrDefaultAsync(m => m.Id.ToString().Equals(service.GeneratedFrom));
                    if (oldService != null)
                    {
                        oldService.IsLocked = false;
                        oldService.Status = ServiceStatus.Obsolete;
                        _context.Service.Update(oldService);
                    }
                }

                _context.Service.Update(service);
                AddAuditLog(service.Id, "Approved", "UnderReview", "Approved");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Service approved successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ServiceExists(service.Id))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Services/SubmitForReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(int id)
        {
            var service = await _context.Service.FirstOrDefaultAsync(x => x.Id == id);
            if (service == null) return NotFound();

            if (service.Status != ServiceStatus.Draft)
            {
                TempData["WorkflowError"] = "Only Draft services can be submitted for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            service.Status = ServiceStatus.UnderReview;
            _context.Service.Update(service);
            AddAuditLog(service.Id, "Submitted for review", "Draft", "UnderReview");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Service submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Services/SendBackToDraft/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBackToDraft(int id)
        {
            var service = await _context.Service.FirstOrDefaultAsync(x => x.Id == id);
            if (service == null) return NotFound();

            var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.AuthorisedPersonRole);
            if (!isAuthorized)
            {
                TempData["WorkflowError"] = "You do not have permission to send back for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (service.Status != ServiceStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only services under review can be sent back to draft.";
                return RedirectToAction(nameof(Details), new { id });
            }

            service.Status = ServiceStatus.Draft;
            _context.Service.Update(service);
            AddAuditLog(service.Id, "Sent back to draft", "UnderReview", "Draft");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Service sent back to Draft for rework.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Services/Create
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public IActionResult Create()
        {
            PopulateServiceRelatedDocsViewData();
            return View();
        }

        // POST: Services/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Create([Bind("ServiceId,Name,Description,Category")] Service service, int[]? selectedRelatedDocs)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var prevId = _context.Service
                        .AsEnumerable()
                        .Where(x => !string.IsNullOrEmpty(x.ServiceId))
                        .Select(x => {
                            var parts = x.ServiceId.Split('-');
                            return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    service.ServiceId = $"SVC-{(prevId + 1).ToString().PadLeft(4, '0')}";
                    service.EditedBy = _userManager.GetUserName(User);
                    service.EditedOn = DateTime.UtcNow;
                    service.Version = Convert.ToDecimal(0.01);
                    service.IsLocked = false;
                    service.Status = ServiceStatus.Draft;

                    _context.Add(service);
                    await _context.SaveChangesAsync();
                    await SyncServiceRelatedDocs(service.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    AddAuditLog(service.Id, "Created", null, "Draft",
                        $"ID: {service.ServiceId}; Name: {service.Name}");
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id = service.Id });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            PopulateServiceRelatedDocsViewData(selectedRelatedDocs);
            return View(service);
        }

        // GET: Services/Edit/5
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Service == null)
                return NotFound();

            var service = await _context.Service
                .Include(s => s.RelatedDocuments!)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (service == null)
                return NotFound();

            if (service.IsLocked || service.Status == ServiceStatus.Obsolete)
                return Forbid();

            if (service.Status == ServiceStatus.UnderReview)
            {
                TempData["Error"] = "This service is currently under review and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var relDocIds = service.RelatedDocuments?.Select(rd => rd.AppDocumentId).ToArray();
            PopulateServiceRelatedDocsViewData(relDocIds);
            return View(service);
        }

        // POST: Services/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ServiceId,GeneratedFrom,Name,Description,Category,Version,EditedBy,EditedOn,ApprovedBy,ApprovedOn,IsLocked,Status")] Service service, int[]? selectedRelatedDocs)
        {
            if (id != service.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Lock/status decisions and system-managed fields come from the stored record,
                    // never from the posted (hidden) form values.
                    var current = await _context.Service.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    if (current == null) return NotFound();
                    if (current.IsLocked || current.Status == ServiceStatus.Obsolete) return Forbid();
                    if (current.Status == ServiceStatus.UnderReview)
                    {
                        TempData["Error"] = "This service is currently under review and cannot be edited.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    service.IsLocked      = current.IsLocked;
                    service.Status        = current.Status;
                    service.Version       = current.Version;
                    service.ApprovedBy    = current.ApprovedBy;
                    service.ApprovedOn    = current.ApprovedOn;
                    service.GeneratedFrom = current.GeneratedFrom;
                    service.ServiceId = current.ServiceId;
                    service.IsDeleted     = current.IsDeleted;
                    service.DeletedBy     = current.DeletedBy;
                    service.DeletedOn     = current.DeletedOn;

                    // Field-level change tracking for the audit log
                    var snap = await _context.Service.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff,     "Name",        snap.Name,        service.Name);
                        AuditDiffHelper.Track(diff,     "Description", snap.Description, service.Description);
                        AuditDiffHelper.TrackEnum(diff, "Category",    snap.Category,    service.Category);
                    }
                    var editNotes = AuditDiffHelper.Build(diff);

                    service.Version += Convert.ToDecimal(0.01);
                    service.EditedBy = _userManager.GetUserName(User);
                    service.EditedOn = DateTime.UtcNow;
                    service.ExportFilePath = string.Empty;

                    if (service.Status == ServiceStatus.Approved)
                    {
                        using (var transaction = _context.Database.BeginTransaction())
                        {
                            service.Id = _context.Service.Max(m => m.Id) + 1;
                            service.Status = ServiceStatus.Draft;
                            service.ApprovedBy = null;
                            service.ApprovedOn = null;
                            service.GeneratedFrom = id.ToString();
                            service.IsLocked = false;
                            _context.Add(service);
                            _context.SaveChanges();
                            transaction.Commit();
                        }

                        Service? service1 = await _context.Service.FirstOrDefaultAsync(m => m.Id == id);
                        service1.IsLocked = true;
                        _context.Update(service1);
                        await _context.SaveChangesAsync();
                        AddAuditLog(service.Id, "Edited (new revision)", "Approved", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                        await SyncServiceRelatedDocs(service.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    }
                    else
                    {
                        service.Status = ServiceStatus.Draft;
                        _context.Update(service);
                        if (diff.Count > 0) AddAuditLog(service.Id, "Edited", "Draft", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                        await SyncServiceRelatedDocs(service.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceExists(service.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Details), new { id = service.Id });
            }

            PopulateServiceRelatedDocsViewData(selectedRelatedDocs);
            return View(service);
        }

        // GET: Services/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Service == null)
                return NotFound();

            var service = await _context.Service
                .Include(s => s.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (service == null)
                return NotFound();

            if (service.Status != ServiceStatus.Draft)
            {
                TempData["Error"] = "Only Draft services can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            var hasSupplierLinks = _context.SupplierService.Any(x => x.ServiceId == id);
            ViewData["HasLinkedRecords"] = service.Documents?.Any() == true || hasSupplierLinks;

            return View(service);
        }

        // POST: Services/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Service == null)
                return Problem("Entity set 'ApplicationDbContext.Service' is null.");

            var service = await _context.Service
                .Include(s => s.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (service == null)
                return NotFound();

            if (service.Status != ServiceStatus.Draft)
            {
                TempData["Error"] = "Only Draft services can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (service.Documents?.Any() == true)
            {
                TempData["ErrorMessage"] = "This service has uploaded Documents. Remove them before deleting.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            if (_context.SupplierService.Any(x => x.ServiceId == id))
            {
                TempData["ErrorMessage"] = "This service is linked to one or more Suppliers. Remove those links before deleting.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            AddAuditLog(service.Id, "Deleted", service.Status.ToString(), null, service.Name);
            service.IsDeleted = true;
            service.DeletedBy = _userManager.GetUserName(User);
            service.DeletedOn = DateTime.UtcNow;
            _context.Service.Update(service);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Services/Documents/5
        public async Task<IActionResult> Documents(int? id)
        {
            if (id == null)
                return NotFound();

            var service = await _context.Service
                .Include(s => s.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (service == null)
                return NotFound();

            // Recalculate document statuses
            if (service.Documents != null)
            {
                bool changed = false;
                foreach (var doc in service.Documents)
                {
                    ServiceDocumentStatus newStatus;
                    if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow)
                        newStatus = ServiceDocumentStatus.Expired;
                    else if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow.AddDays(doc.ReminderDays))
                        newStatus = ServiceDocumentStatus.Expiring;
                    else
                        newStatus = ServiceDocumentStatus.Valid;

                    if (doc.DocumentStatus != newStatus)
                    {
                        doc.DocumentStatus = newStatus;
                        changed = true;
                    }
                }
                if (changed)
                    await _context.SaveChangesAsync();
            }

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.ServiceDocument.DeletedOnly().Where(f => f.ServiceId == id).ToListAsync();
            return View(service);
        }

        // POST: Services/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> UploadDocument(int serviceId, IFormFile file, string? description, DateTime? issueDate, DateTime? expiryDate)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Documents), new { id = serviceId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.";
                return RedirectToAction(nameof(Documents), new { id = serviceId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds the 25 MB maximum size limit.";
                return RedirectToAction(nameof(Documents), new { id = serviceId });
            }

            var service = await _context.Service.FindAsync(serviceId);
            if (service == null)
                return NotFound();

            if (service.Status != ServiceStatus.Draft && service.Status != ServiceStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be uploaded to Draft or Under Review services.";
                return RedirectToAction(nameof(Documents), new { id = serviceId });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "services", serviceId.ToString());
            Directory.CreateDirectory(uploadDir);

            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var doc = new ServiceDocument
            {
                ServiceId = serviceId,
                FileName = file.FileName,
                FilePath = $"/uploads/services/{serviceId}/{uniqueName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = description,
                UploadedBy = _userManager.GetUserName(User),
                UploadedDate = DateTime.UtcNow,
                DocumentStatus = ServiceDocumentStatus.Valid
            };

            _context.ServiceDocument.Add(doc);
            AddAuditLog(serviceId, "Document uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(Documents), new { id = serviceId });
        }

        // GET: Services/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.ServiceDocument.ForDownload(deleted)
                .Include(d => d.Service)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null)
                return NotFound();

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(fullPath, doc.FileHash))
            {
                AddAuditLog(doc.ServiceId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction("Documents", new { id = doc.ServiceId });
            }

            string recordId = doc.Service?.ServiceId ?? doc.ServiceId.ToString();
            string? version = doc.Service?.Version.ToString("0.##");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Service",
                recordId, version,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        // POST: Services/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> DeleteDocument(int id, int serviceId, string? deletionReason)
        {
            var doc = await _context.ServiceDocument.FindAsync(id);
            if (doc == null)
                return NotFound();

            var service = await _context.Service.FindAsync(doc.ServiceId);
            if (service == null)
                return NotFound();

            if (service.Status != ServiceStatus.Draft && service.Status != ServiceStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be deleted from Draft or Under Review services.";
                return RedirectToAction(nameof(Documents), new { id = doc.ServiceId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                return RedirectToAction(nameof(Documents), new { id = doc.ServiceId });
            }

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();
            AddAuditLog(doc.ServiceId, "Document deleted", null, null, $"{doc.FileName} — Reason: {doc.DeletionReason}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Documents), new { id = doc.ServiceId });
        }

        public ActionResult GetDocuments()
        {
            var services = _context.Service.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(ServiceStatus)).Cast<ServiceStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            if (services != null && services.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = services.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);
            return Json(new { dataSet = dataSets, labels });
        }

        public ActionResult GetDocumentsForBarchart()
        {
            var services = _context.Service.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();
            var dates = services.Select(x => x.EditedOn.ToSite().Date).Distinct().ToList();

            if (services != null && services.Count > 0)
            {
                foreach (var date in dates)
                {
                    labels.Add(date.ToShortDateString());
                    var serviceCount = services.Where(x => x.EditedOn.ToSite().Date == date).Count();
                    dataSet.Data.Add(serviceCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);
            return Json(new { dataSet = dataSets, labels });
        }

        public async Task<IActionResult> ExportServiceDetail(int id)
        {
            var service = await _context.Service.Where(x => x.Id == id).FirstOrDefaultAsync();

            if (service == null)
                return NotFound();

            var fields = new List<(string, string?)>
            {
                ("Service ID",  service.ServiceId),
                ("Name",        service.Name),
                ("Status",      service.Status.ToString()),
                ("Category",    service.Category.ToString()),
                ("Version",     service.Version.ToString()),
                ("Description", service.Description),
                ("Edited By",   service.EditedBy),
                ("Edited On",   service.EditedOn.ToSite().ToShortDateString()),
                ("Approved By", service.ApprovedBy),
                ("Approved On", service.ApprovedOn.ToSite()?.ToShortDateString()),
            };

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Service Detail Report", service.ServiceId, service.Status.ToString(), fields,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Service-{service.ServiceId}.pdf");
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            Service? service = _context.Service.Where(x => x.Id == id).FirstOrDefault();

            if (service == null)
                return NotFound();

            var builder = new StringBuilder();
            builder.AppendLine("Service Id,Name,Description,Category,Version,Status,Edited By,Edited On,Approved By,Approved On");
            builder.AppendLine($"{service.ServiceId},{service.Name},{service.Description},{service.Category},{service.Version},{service.Status},{(service.EditedBy != null ? service.EditedBy : String.Empty)},{service.EditedOn.ToSite().ToShortDateString()},{(service.ApprovedBy != null ? service.ApprovedBy : String.Empty)},{(service.ApprovedOn != null ? service.ApprovedOn.Value.ToSite().ToShortDateString() : String.Empty)}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Service.csv");
        }

        private bool ServiceExists(int id)
        {
            return (_context.Service?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MasterDataAuditLog.Add(new MasterDataAuditLog
            {
                EntityType = "Service",
                ParentId = parentId,
                Action = action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                PerformedBy = User.Identity?.Name ?? "System",
                PerformedOn = DateTime.UtcNow,
                Notes = notes
            });
        }
    }
}
