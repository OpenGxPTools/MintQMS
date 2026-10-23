/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2024  C-realize IT Services SRL (https://www.c-realize.com)

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

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.ViewModels;
using OpenQMS.Services;
using static OpenQMS.Models.Process;
using static OpenQMS.Models.Product;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class ProcessesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        public ProcessesController
             (
                 ApplicationDbContext context,
                 UserManager<AppUser> userManager,
                 SignInManager<AppUser> signInManager
             )
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        private async Task SyncProcessJunctions(int processId, int[] productIds)
        {
            _context.ProcessProduct.RemoveRange(await _context.ProcessProduct.Where(x => x.ProcessId == processId).ToListAsync());
            foreach (var id in productIds.Distinct())
                _context.ProcessProduct.Add(new ProcessProduct { ProcessId = processId, ProductId = id });
        }

        private async Task SyncProcessRelatedDocs(int processId, int[] docIds)
        {
            _context.ProcessLinkedDocument.RemoveRange(
                await _context.ProcessLinkedDocument.Where(x => x.ProcessId == processId).ToListAsync());
            foreach (var docId in docIds.Distinct())
            {
                var doc = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null)
                    _context.ProcessLinkedDocument.Add(new ProcessLinkedDocument
                        { ProcessId = processId, AppDocumentId = docId, LinkedVersion = doc.Version });
            }
        }

        private void PopulateProcessRelatedDocsViewData(int[]? selectedIds = null)
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

        // GET: Processes
        public async Task<IActionResult> Index()
        {
            var processList = await _context.Process.Where(p => !p.IsDeleted).Include(c => c.LinkedProducts).ThenInclude(x => x.Product).ToListAsync();
            return View(processList);
        }

        // GET: Processes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Process == null)
            {
                return NotFound();
            }

            var process = await _context.Process
                .Include(a => a.LinkedProducts).ThenInclude(x => x.Product)
                .Include(a => a.Assets)
                .Include(a => a.Materials)
                .Include(a => a.Deviations)
                .Include(a => a.Changes)
                .Include(a => a.Capas)
                .Include(a => a.LinkedComplaints).ThenInclude(cp => cp.Complaint)
                .Include(a => a.Documents)
                .Include(a => a.RelatedDocuments!).ThenInclude(rd => rd.Document)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (process == null)
            {
                return NotFound();
            }

            ViewBag.AuditLogs = await _context.MasterDataAuditLog
                .Where(l => l.EntityType == "Process" && l.ParentId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            return View(process);
        }

        // POST: Processes/Details/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, string InputEmail, string InputPassword)
        {
            var process = _context.Process.FirstOrDefault(x => x.Id == id);

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

                if (process.Status != ProcessStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only processes under review can be approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                process.Version = decimal.ToInt32(process.Version) + 1;
                process.ApprovedBy = _userManager.GetUserName(User);
                process.ApprovedOn = DateTime.UtcNow;
                process.Status = ProcessStatus.Approved;
                process.ExportFilePath = string.Empty;

                if (process.GeneratedFrom != null && !string.IsNullOrEmpty(process.GeneratedFrom))
                {
                    int.TryParse(process.GeneratedFrom, out var generatedFromId);
                    var oldProcess = await _context.Process.FirstOrDefaultAsync(m => m.Id == generatedFromId);
                    if (oldProcess != null)
                    {
                        oldProcess.IsLocked = false;
                        oldProcess.Status = ProcessStatus.Obsolete;
                        oldProcess.ExportFilePath = string.Empty;
                        _context.Process.Update(oldProcess);
                    }
                }

                _context.Process.Update(process);
                AddAuditLog(process.Id, "Approved", "UnderReview", "Approved");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Process approved successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProcessExists(process.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Processes/SubmitForReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(int id)
        {
            var process = await _context.Process.FirstOrDefaultAsync(x => x.Id == id);
            if (process == null) return NotFound();

            if (process.Status != ProcessStatus.Draft)
            {
                TempData["WorkflowError"] = "Only Draft processes can be submitted for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            process.Status = ProcessStatus.UnderReview;
            _context.Process.Update(process);
            AddAuditLog(process.Id, "Submitted for review", "Draft", "UnderReview");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Process submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Processes/SendBackToDraft/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBackToDraft(int id)
        {
            var process = await _context.Process.FirstOrDefaultAsync(x => x.Id == id);
            if (process == null) return NotFound();

            var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.AuthorisedPersonRole);
            if (!isAuthorized)
            {
                TempData["WorkflowError"] = "You do not have permission to send back for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (process.Status != ProcessStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only processes under review can be sent back to draft.";
                return RedirectToAction(nameof(Details), new { id });
            }

            process.Status = ProcessStatus.Draft;
            _context.Process.Update(process);
            AddAuditLog(process.Id, "Sent back to draft", "UnderReview", "Draft");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Process sent back to Draft for rework.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Processes/Create
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public IActionResult Create()
        {
            ViewData["Products"] = _context.Product
                .Where(x => !x.IsDeleted && x.Status.Equals(Product.ProductStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewBag.SelectedProductIds = new List<int>();
            PopulateProcessRelatedDocsViewData();
            return View();
        }

        // POST: Processes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Create([Bind("ProcessId,Name,Description,Category")] Process process, int[]? selectedProducts, int[]? selectedRelatedDocs)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var prevId = _context.Process
                        .AsEnumerable()
                        .Where(x => !string.IsNullOrEmpty(x.ProcessId))
                        .Select(x => {
                            var parts = x.ProcessId.Split('-');
                            return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    prevId = prevId + 1;
                    process.ProcessId = $"PRC-{prevId.ToString().PadLeft(4, '0')}";
                    process.EditedBy = _userManager.GetUserName(User);
                    process.EditedOn = DateTime.UtcNow;
                    process.Version = Convert.ToDecimal(0.01);
                    process.IsLocked = false;

                    _context.Add(process);
                    await _context.SaveChangesAsync();

                    await SyncProcessJunctions(process.Id, selectedProducts ?? Array.Empty<int>());
                    await SyncProcessRelatedDocs(process.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    AddAuditLog(process.Id, "Created", null, "Draft",
                        $"ID: {process.ProcessId}; Name: {process.Name}");
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id = process.Id });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            ViewData["Products"] = _context.Product
                .Where(x => !x.IsDeleted && x.Status.Equals(ProductStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewBag.SelectedProductIds = selectedProducts?.ToList() ?? new List<int>();
            PopulateProcessRelatedDocsViewData(selectedRelatedDocs);
            return View(process);
        }

        // GET: Processes/Edit/5
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Process == null)
            {
                return NotFound();
            }

            var process = await _context.Process
                .Include(p => p.LinkedProducts).ThenInclude(x => x.Product)
                .Include(p => p.RelatedDocuments!)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (process == null)
            {
                return NotFound();
            }

            if (process.IsLocked || process.Status == ProcessStatus.Obsolete)
            {
                return Forbid();
            }

            if (process.Status == ProcessStatus.UnderReview)
            {
                TempData["Error"] = "This process is currently under review and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["Products"] = _context.Product
                .Where(x => !x.IsDeleted && x.Status.Equals(ProductStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewBag.SelectedProductIds = process.LinkedProducts?.Any() == true
                ? process.LinkedProducts.Select(x => x.ProductId).ToList()
                : (process.ProductId.HasValue ? new List<int> { process.ProductId.Value } : new List<int>());
            var relDocIds = process.RelatedDocuments?.Select(rd => rd.AppDocumentId).ToArray();
            PopulateProcessRelatedDocsViewData(relDocIds);
            return View(process);
        }

        // POST: Processes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProcessId,GeneratedFrom,Name,Description,Version,EditedBy,EditedOn,ApprovedBy,ApprovedOn,Category")] Process process, int[]? selectedProducts, int[]? selectedRelatedDocs)
        {
            if (id != process.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var current = await _context.Process.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                    if (current == null)
                    {
                        return NotFound();
                    }

                    if (current.IsLocked || current.Status == ProcessStatus.Obsolete)
                    {
                        return Forbid();
                    }
                    if (current.Status == ProcessStatus.UnderReview)
                    {
                        TempData["Error"] = "This process is currently under review and cannot be edited.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    // System-managed fields come from the stored record, never from posted (hidden) values
                    process.IsLocked      = current.IsLocked;
                    process.Status        = current.Status;
                    process.Version       = current.Version;
                    process.ApprovedBy    = current.ApprovedBy;
                    process.ApprovedOn    = current.ApprovedOn;
                    process.GeneratedFrom = current.GeneratedFrom;
                    process.ProcessId = current.ProcessId;
                    process.IsDeleted     = current.IsDeleted;
                    process.DeletedBy     = current.DeletedBy;
                    process.DeletedOn     = current.DeletedOn;

                    // Field-level change tracking for the audit log
                    var diff = new List<string>();
                    AuditDiffHelper.Track(diff,     "Name",        current.Name,        process.Name);
                    AuditDiffHelper.Track(diff,     "Description", current.Description, process.Description);
                    AuditDiffHelper.TrackEnum(diff, "Category",    current.Category,    process.Category);
                    // Linked-record changes (queried before the sync below, so these are the old associations)
                    var oldPrProductIds = await _context.ProcessProduct.Where(x => x.ProcessId == id).Select(x => x.ProductId).ToListAsync();
                    var oldPrDocIds     = await _context.ProcessLinkedDocument.Where(x => x.ProcessId == id).Select(x => x.AppDocumentId).ToListAsync();
                    AuditDiffHelper.TrackSet(diff, "Linked Products",  oldPrProductIds, selectedProducts    ?? Array.Empty<int>(),
                        ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                    AuditDiffHelper.TrackSet(diff, "Linked Documents", oldPrDocIds,     selectedRelatedDocs ?? Array.Empty<int>(),
                        ids => _context.AppDocument.Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id, d => d.DocumentId));
                    var editNotes = AuditDiffHelper.Build(diff);

                    process.Version += Convert.ToDecimal(0.01);
                    process.EditedBy = _userManager.GetUserName(User);
                    process.EditedOn = DateTime.UtcNow;
                    process.ExportFilePath = string.Empty;

                    if (current.Status == ProcessStatus.Approved)
                    {
                        process.Id = _context.Process.Max(m => m.Id) + 1;
                        process.Status = ProcessStatus.Draft;
                        process.ApprovedBy = string.Empty;
                        process.ApprovedOn = null;
                        process.GeneratedFrom = id.ToString();
                        process.IsLocked = false;
                        _context.Add(process);
                        _context.SaveChanges();

                        Process? process1 = await _context.Process.FirstOrDefaultAsync(m => m.Id == id);
                        process1.IsLocked = true;
                        _context.Update(process1);
                        await _context.SaveChangesAsync();
                        await SyncProcessJunctions(process.Id, selectedProducts ?? Array.Empty<int>());
                        await SyncProcessRelatedDocs(process.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        AddAuditLog(process.Id, "Edited (new revision)", "Approved", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        process.Status = ProcessStatus.Draft;
                        process.IsLocked = current.IsLocked;
                        _context.Update(process);
                        await SyncProcessJunctions(process.Id, selectedProducts ?? Array.Empty<int>());
                        await SyncProcessRelatedDocs(process.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        if (diff.Count > 0) AddAuditLog(process.Id, "Edited", "Draft", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProcessExists(process.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Details), new { id = process.Id });
            }

            PopulateProcessRelatedDocsViewData(selectedRelatedDocs);
            return View(process);
        }

        // GET: Processes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Process == null)
            {
                return NotFound();
            }

            var process = await _context.Process
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (process == null)
            {
                return NotFound();
            }

            if (process.Status != ProcessStatus.Draft)
            {
                TempData["Error"] = "Only Draft processes can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["HasLinkedRecords"] = process.Changes?.Any() == true
                || process.Deviations?.Any() == true
                || process.Capas?.Any() == true
                || process.Documents?.Any() == true;

            return View(process);
        }

        // POST: Processes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Process == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Process'  is null.");
            }

            var process = await _context.Process
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (process.Status != ProcessStatus.Draft)
            {
                TempData["Error"] = "Only Draft processes can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (process.Changes?.Any() == true || process.Deviations?.Any() == true || process.Capas?.Any() == true || process.Documents?.Any() == true)
            {
                TempData["ErrorMessage"] = "This process has linked Changes, Deviations, CAPAs, or uploaded Documents. Remove them before deleting.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            if (process != null)
            {
                AddAuditLog(process.Id, "Deleted", process.Status.ToString(), null, process.Name);
                process.IsDeleted = true;
                process.DeletedBy = _userManager.GetUserName(User);
                process.DeletedOn = DateTime.UtcNow;
                _context.Process.Update(process);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Processes/Documents/5
        public async Task<IActionResult> Documents(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var process = await _context.Process
                .Include(p => p.Documents)
                .Include(p => p.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (process == null)
            {
                return NotFound();
            }

            // Recalculate document statuses
            if (process.Documents != null)
            {
                bool changed = false;
                foreach (var doc in process.Documents)
                {
                    ProcessDocumentStatus newStatus;
                    if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow)
                        newStatus = ProcessDocumentStatus.Expired;
                    else if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow.AddDays(doc.ReminderDays))
                        newStatus = ProcessDocumentStatus.Expiring;
                    else
                        newStatus = ProcessDocumentStatus.Valid;

                    if (doc.DocumentStatus != newStatus)
                    {
                        doc.DocumentStatus = newStatus;
                        changed = true;
                    }
                }
                if (changed)
                {
                    await _context.SaveChangesAsync();
                }
            }

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.ProcessDocument.DeletedOnly().Where(f => f.ProcessId == id).ToListAsync();
            return View(process);
        }

        // POST: Processes/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> UploadDocument(int processId, IFormFile file, string? description, DateTime? issueDate, DateTime? expiryDate)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Documents), new { id = processId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.";
                return RedirectToAction(nameof(Documents), new { id = processId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds the 25 MB maximum size limit.";
                return RedirectToAction(nameof(Documents), new { id = processId });
            }

            var process = await _context.Process.FindAsync(processId);
            if (process == null)
            {
                return NotFound();
            }

            if (process.Status != ProcessStatus.Draft && process.Status != ProcessStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be uploaded to Draft or Under Review processes.";
                return RedirectToAction(nameof(Documents), new { id = processId });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "processes", processId.ToString());
            Directory.CreateDirectory(uploadDir);

            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var doc = new ProcessDocument
            {
                ProcessId = processId,
                FileName = file.FileName,
                FilePath = $"/uploads/processes/{processId}/{uniqueName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = description,
                UploadedBy = _userManager.GetUserName(User),
                UploadedDate = DateTime.UtcNow,
                DocumentStatus = ProcessDocumentStatus.Valid
            };

            _context.ProcessDocument.Add(doc);
            AddAuditLog(processId, "Document uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(Documents), new { id = processId });
        }

        // GET: Processes/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.ProcessDocument.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (doc == null)
            {
                return NotFound();
            }

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(fullPath, doc.FileHash))
            {
                AddAuditLog(doc.ProcessId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction("Documents", new { id = doc.ProcessId });
            }

            var parent  = await _context.Process.FindAsync(doc.ProcessId);
            var version = parent?.Version.ToString("0.##");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Process",
                doc.ProcessId.ToString(), version,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        // POST: Processes/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> DeleteDocument(int id, int processId, string? deletionReason)
        {
            var doc = await _context.ProcessDocument.FindAsync(id);
            if (doc == null)
            {
                return NotFound();
            }

            var process = await _context.Process.FindAsync(doc.ProcessId);
            if (process == null)
            {
                return NotFound();
            }

            if (process.Status != ProcessStatus.Draft && process.Status != ProcessStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be deleted from Draft or Under Review processes.";
                return RedirectToAction(nameof(Documents), new { id = doc.ProcessId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                return RedirectToAction(nameof(Documents), new { id = doc.ProcessId });
            }

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();
            AddAuditLog(doc.ProcessId, "Document deleted", null, null, $"{doc.FileName} — Reason: {doc.DeletionReason}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Documents), new { id = doc.ProcessId });
        }

        public ActionResult GetDocuments()
        {
            var processes = _context.Process.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(ProcessStatus)).Cast<ProcessStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            if (processes != null && processes.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = processes.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public ActionResult GetDocumentsForBarchart()
        {
            var processes = _context.Process.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();
            var dates = processes.Select(x => x.EditedOn.ToSite().Date).Distinct().ToList();

            if (processes != null && processes.Count > 0)
            {
                foreach (var date in dates)
                {
                    labels.Add(date.ToShortDateString());
                    var processCount = processes.Where(x => x.EditedOn.ToSite().Date == date).Count();
                    dataSet.Data.Add(processCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public async Task<IActionResult> ExportProcessDetail(int id)
        {
            var process = await _context.Process
                .Where(x => x.Id == id)
                .Include(a => a.LinkedProducts).ThenInclude(x => x.Product)
                .Include(a => a.Assets)
                .Include(a => a.Materials)
                .Include(a => a.Changes)
                .Include(a => a.Deviations)
                .Include(a => a.Capas)
                .FirstOrDefaultAsync();

            if (process == null)
            {
                return NotFound();
            }

            var assetList = process.Assets != null ? process.Assets.ToList() : new List<Asset>();
            var materialList = process.Materials != null ? process.Materials.ToList() : new List<Material>();
            var changeList = process.Changes != null ? process.Changes.ToList() : new List<Change>();
            var deviationList = process.Deviations != null ? process.Deviations.ToList() : new List<Deviation>();
            var capaList = process.Capas != null ? process.Capas.ToList() : new List<Capa>();
            var fields = new List<(string, string?)>
            {
                ("Process ID",  process.ProcessId),
                ("Name",        process.Name),
                ("Status",      process.Status.ToString()),
                ("Category",    process.Category.HasValue
                                ? process.Category.Value.GetType()
                                    .GetField(process.Category.Value.ToString())
                                    ?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                                    .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>()
                                    .FirstOrDefault()?.Name ?? process.Category.Value.ToString()
                                : null),
                ("Description", process.Description),
                ("Product",     process.LinkedProducts?.Any() == true
                                ? string.Join(", ", process.LinkedProducts.Select(x => x.Product?.Name ?? ""))
                                : process.Product?.Name),
                ("Edited By",   process.EditedBy),
                ("Edited On",   process.EditedOn.ToSite().ToShortDateString()),
                ("Approved By", process.ApprovedBy),
                ("Approved On", process.ApprovedOn.ToSite()?.ToShortDateString()),
            };

            var tables = new List<(string, string[], IEnumerable<string[]>)>
            {
                ("Assets",
                 new[] { "Asset ID", "Name", "Status" },
                 assetList.Select(a => new[] { a.AssetId, a.Name, a.Status.ToString() })),
                ("Materials",
                 new[] { "Material ID", "Name", "Status" },
                 materialList.Select(m => new[] { m.MaterialId, m.Name, m.Status.ToString() })),
                ("Changes",
                 new[] { "Change ID", "Title", "Status" },
                 changeList.Select(c => new[] { c.ChangeId, c.Title, c.Status.ToString() })),
                ("Deviations",
                 new[] { "Deviation ID", "Title", "Status" },
                 deviationList.Select(d => new[] { d.DeviationId, d.Title, d.Status.ToString() })),
                ("CAPAs",
                 new[] { "CAPA ID", "Title", "Status" },
                 capaList.Select(c => new[] { c.CapaId, c.Title, c.Status.ToString() })),
            };

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Process Detail Report", process.ProcessId, process.Status.ToString(), fields, tables,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Process-{process.ProcessId}.pdf");
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            Process? process = _context.Process.Where(x => x.Id == id).Include(x => x.LinkedProducts).ThenInclude(x => x.Product).FirstOrDefault();

            if (process == null)
            {
                return NotFound();
            }

            var builder = new StringBuilder();
            builder.AppendLine("Process Id,Name,Description,Category,Product,Version,Status,Edited By,Edited On,Approved By,Approved On");
            var processProductNames = process.LinkedProducts?.Any() == true
                ? string.Join("|", process.LinkedProducts.Select(x => x.Product?.Name ?? ""))
                : (process.Product?.Name ?? string.Empty);
            builder.AppendLine($"{process.ProcessId},{process.Name},{process.Description},{process.Category?.ToString() ?? ""},{processProductNames},{process.Version},{process.Status},{(process.EditedBy != null ? process.EditedBy : String.Empty)},{process.EditedOn.ToSite().ToShortDateString()},{(process.ApprovedBy != null ? process.ApprovedBy : String.Empty)},{(process.ApprovedOn != null ? process.ApprovedOn.Value.ToSite().ToShortDateString() : String.Empty)}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Process.csv");
        }

        private bool ProcessExists(int id)
        {
            return _context.Process.Any(e => e.Id == id);
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MasterDataAuditLog.Add(new MasterDataAuditLog
            {
                EntityType = "Process",
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
