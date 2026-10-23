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
using static OpenQMS.Models.Asset;
using static OpenQMS.Models.Process;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class AssetsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        public AssetsController
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

        private async Task SyncAssetJunctions(int assetId, int[] processIds)
        {
            _context.AssetProcess.RemoveRange(await _context.AssetProcess.Where(x => x.AssetId == assetId).ToListAsync());
            foreach (var id in processIds.Distinct())
                _context.AssetProcess.Add(new AssetProcess { AssetId = assetId, ProcessId = id });
        }

        private async Task SyncAssetRelatedDocs(int assetId, int[] docIds)
        {
            _context.AssetLinkedDocument.RemoveRange(
                await _context.AssetLinkedDocument.Where(x => x.AssetId == assetId).ToListAsync());
            foreach (var docId in docIds.Distinct())
            {
                var doc = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null)
                    _context.AssetLinkedDocument.Add(new AssetLinkedDocument
                        { AssetId = assetId, AppDocumentId = docId, LinkedVersion = doc.Version });
            }
        }

        private void PopulateRelatedDocsViewData(int[]? selectedIds = null)
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

        // GET: Assets
        public async Task<IActionResult> Index()
        {
            var assetsList = await _context.Asset.Where(a => !a.IsDeleted).Include(c => c.LinkedProcesses).ThenInclude(x => x.Process).ToListAsync();
            return View(assetsList);
        }

        // GET: Assets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Asset == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset
                .Include(a => a.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(a => a.Changes)
                .Include(a => a.Deviations)
                .Include(a => a.Capas)
                .Include(a => a.Documents)
                .Include(a => a.RelatedDocuments!).ThenInclude(rd => rd.Document)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (asset == null)
            {
                return NotFound();
            }

            ViewBag.AuditLogs = await _context.MasterDataAuditLog
                .Where(l => l.EntityType == "Asset" && l.ParentId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            return View(asset);
        }

        // POST: Assets/Details/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, string InputEmail, string InputPassword)
        {
            var asset = _context.Asset.FirstOrDefault(x => x.Id == id);

            try
            {
                var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole);
                if (!isAuthorized)
                {
                    TempData["WorkflowError"] = "You do not have permission to approve.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var userSigning = await _userManager.FindByEmailAsync(InputEmail);
                var loggedUser = _userManager.GetUserName(User);
                if ((userSigning == null) || (userSigning.UserName != loggedUser))
                {
                    TempData["WorkflowError"] = "Invalid credentials. Please enter your own email and password.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var result = await _signInManager.PasswordSignInAsync(userSigning.UserName, InputPassword, isPersistent: false, lockoutOnFailure: false);
                if (!(result.Succeeded || result.RequiresTwoFactor))
                {
                    TempData["WorkflowError"] = "Invalid credentials. Please try again.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (asset.Status != AssetStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only assets under review can be approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                asset.Version = Decimal.ToInt32(asset.Version) + 1;
                asset.ApprovedBy = _userManager.GetUserName(User);
                asset.ApprovedOn = DateTime.UtcNow;
                asset.Status = AssetStatus.Approved;
                asset.ExportFilePath = String.Empty;

                if (asset.GeneratedFrom != null && !string.IsNullOrEmpty(asset.GeneratedFrom))
                {
                    var oldAsset = await _context.Asset.FirstOrDefaultAsync(m => m.Id.ToString().Equals(asset.GeneratedFrom));
                    if (oldAsset != null)
                    {
                        oldAsset.IsLocked = false;
                        oldAsset.Status = AssetStatus.Obsolete;
                        oldAsset.ExportFilePath = String.Empty;
                        _context.Asset.Update(oldAsset);
                    }
                }

                _context.Asset.Update(asset);
                AddAuditLog(asset.Id, "Approved", "UnderReview", "Approved");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Asset approved successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AssetExists(asset.Id))
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

        // POST: Assets/SubmitForReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(int id)
        {
            var asset = await _context.Asset.FirstOrDefaultAsync(x => x.Id == id);
            if (asset == null) return NotFound();

            if (asset.Status != AssetStatus.Draft)
            {
                TempData["WorkflowError"] = "Only Draft assets can be submitted for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            asset.Status = AssetStatus.UnderReview;
            _context.Asset.Update(asset);
            AddAuditLog(asset.Id, "Submitted for review", "Draft", "UnderReview");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Asset submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Assets/SendBackToDraft/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBackToDraft(int id)
        {
            var asset = await _context.Asset.FirstOrDefaultAsync(x => x.Id == id);
            if (asset == null) return NotFound();

            var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole);
            if (!isAuthorized)
            {
                TempData["WorkflowError"] = "You do not have permission to send back for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (asset.Status != AssetStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only assets under review can be sent back to draft.";
                return RedirectToAction(nameof(Details), new { id });
            }

            asset.Status = AssetStatus.Draft;
            _context.Asset.Update(asset);
            AddAuditLog(asset.Id, "Sent back to draft", "UnderReview", "Draft");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Asset sent back to Draft for rework.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Assets/Create
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public IActionResult Create()
        {
            ViewData["Processes"] = _context.Process
                .Where(x => !x.IsDeleted && x.Status.Equals(ProcessStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewBag.SelectedProcessIds = new List<int>();
            PopulateRelatedDocsViewData();
            return View();
        }

        // POST: Assets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Create([Bind("AssetId,Name,Description,Category")] Asset asset, int[]? selectedProcesses, int[]? selectedRelatedDocs)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var prevId = _context.Asset
                        .AsEnumerable()
                        .Where(x => !string.IsNullOrEmpty(x.AssetId))
                        .Select(x => {
                            var parts = x.AssetId.Split('-');
                            return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    asset.AssetId = $"AST-{(prevId + 1).ToString().PadLeft(4, '0')}";
                    asset.EditedBy = _userManager.GetUserName(User);
                    asset.EditedOn = DateTime.UtcNow;
                    asset.Version = Convert.ToDecimal(0.01);
                    asset.IsLocked = false;

                    _context.Add(asset);
                    await _context.SaveChangesAsync();

                    await SyncAssetJunctions(asset.Id, selectedProcesses ?? Array.Empty<int>());
                    await SyncAssetRelatedDocs(asset.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    AddAuditLog(asset.Id, "Created", null, "Draft",
                        $"ID: {asset.AssetId}; Name: {asset.Name}");
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id = asset.Id });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            ViewData["Processes"] = _context.Process
                .Where(x => !x.IsDeleted && x.Status.Equals(ProcessStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewBag.SelectedProcessIds = selectedProcesses?.ToList() ?? new List<int>();
            PopulateRelatedDocsViewData(selectedRelatedDocs);
            return View(asset);
        }

        // GET: Assets/Edit/5
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Asset == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset
                .Include(a => a.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(a => a.RelatedDocuments!)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (asset == null)
            {
                return NotFound();
            }

            if (asset.IsLocked || asset.Status == AssetStatus.Obsolete)
            {
                return Forbid();
            }

            if (asset.Status == AssetStatus.UnderReview)
            {
                TempData["Error"] = "This asset is currently under review and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["Processes"] = _context.Process
                .Where(x => !x.IsDeleted && x.Status.Equals(ProcessStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewBag.SelectedProcessIds = asset.LinkedProcesses?.Any() == true
                ? asset.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (asset.ProcessId.HasValue ? new List<int> { asset.ProcessId.Value } : new List<int>());
            var relDocIds = asset.RelatedDocuments?.Select(rd => rd.AppDocumentId).ToArray();
            PopulateRelatedDocsViewData(relDocIds);
            return View(asset);
        }

        // POST: Assets/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,AssetId,GeneratedFrom,Name,Description,Version,EditedBy,EditedOn,ApprovedBy,ApprovedOn,IsLocked,Status,Category")] Asset asset, int[]? selectedProcesses, int[]? selectedRelatedDocs)
        {
            if (id != asset.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Lock/status decisions and system-managed fields come from the stored record,
                    // never from the posted (hidden) form values.
                    var current = await _context.Asset.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    if (current == null) return NotFound();
                    if (current.IsLocked || current.Status == AssetStatus.Obsolete) return Forbid();
                    if (current.Status == AssetStatus.UnderReview)
                    {
                        TempData["Error"] = "This asset is currently under review and cannot be edited.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    asset.IsLocked      = current.IsLocked;
                    asset.Status        = current.Status;
                    asset.Version       = current.Version;
                    asset.ApprovedBy    = current.ApprovedBy;
                    asset.ApprovedOn    = current.ApprovedOn;
                    asset.GeneratedFrom = current.GeneratedFrom;
                    asset.AssetId = current.AssetId;
                    asset.IsDeleted     = current.IsDeleted;
                    asset.DeletedBy     = current.DeletedBy;
                    asset.DeletedOn     = current.DeletedOn;

                    // Field-level change tracking for the audit log
                    var snap = await _context.Asset.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff,     "Name",        snap.Name,        asset.Name);
                        AuditDiffHelper.Track(diff,     "Description", snap.Description, asset.Description);
                        AuditDiffHelper.TrackEnum(diff, "Category",    snap.Category,    asset.Category);
                    }
                    // Linked-record changes (queried before the sync below, so these are the old associations)
                    var oldAsProcessIds = await _context.AssetProcess.Where(x => x.AssetId == id).Select(x => x.ProcessId).ToListAsync();
                    var oldAsDocIds     = await _context.AssetLinkedDocument.Where(x => x.AssetId == id).Select(x => x.AppDocumentId).ToListAsync();
                    AuditDiffHelper.TrackSet(diff, "Linked Processes", oldAsProcessIds, selectedProcesses   ?? Array.Empty<int>(),
                        ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                    AuditDiffHelper.TrackSet(diff, "Linked Documents", oldAsDocIds,     selectedRelatedDocs ?? Array.Empty<int>(),
                        ids => _context.AppDocument.Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id, d => d.DocumentId));
                    var editNotes = AuditDiffHelper.Build(diff);

                    asset.Version += Convert.ToDecimal(0.01);
                    asset.EditedBy = _userManager.GetUserName(User);
                    asset.EditedOn = DateTime.UtcNow;
                    asset.ExportFilePath = string.Empty;

                    if (asset.Status == AssetStatus.Approved)
                    {
                        using (var transaction = _context.Database.BeginTransaction())
                        {
                            asset.Id = _context.Asset.Max(m => m.Id) + 1;
                            asset.Status = AssetStatus.Draft;
                            asset.ApprovedBy = null;
                            asset.ApprovedOn = null;
                            asset.GeneratedFrom = id.ToString();
                            asset.IsLocked = false;
                            _context.Add(asset);
                            _context.SaveChanges();
                            transaction.Commit();
                        }

                        Asset? asset1 = await _context.Asset.FirstOrDefaultAsync(m => m.Id == id);
                        asset1.IsLocked = true;
                        _context.Update(asset1);
                        await _context.SaveChangesAsync();
                        await SyncAssetJunctions(asset.Id, selectedProcesses ?? Array.Empty<int>());
                        await SyncAssetRelatedDocs(asset.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        AddAuditLog(asset.Id, "Edited (new revision)", "Approved", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        asset.Status = AssetStatus.Draft;
                        _context.Update(asset);
                        await SyncAssetJunctions(asset.Id, selectedProcesses ?? Array.Empty<int>());
                        await SyncAssetRelatedDocs(asset.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        if (diff.Count > 0) AddAuditLog(asset.Id, "Edited", "Draft", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssetExists(asset.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Details), new { id = asset.Id });
            }

            return View(asset);
        }

        // GET: Assets/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Asset == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (asset == null)
            {
                return NotFound();
            }

            if (asset.Status != AssetStatus.Draft)
            {
                TempData["Error"] = "Only Draft assets can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["HasLinkedRecords"] = asset.Changes?.Any() == true
                || asset.Deviations?.Any() == true
                || asset.Capas?.Any() == true
                || asset.Documents?.Any() == true;

            return View(asset);
        }

        // POST: Assets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Asset == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Asset' is null.");
            }

            var asset = await _context.Asset
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (asset.Status != AssetStatus.Draft)
            {
                TempData["Error"] = "Only Draft assets can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (asset.Changes?.Any() == true || asset.Deviations?.Any() == true
                || asset.Capas?.Any() == true || asset.Documents?.Any() == true)
            {
                TempData["ErrorMessage"] = "This asset has linked Changes, Deviations, CAPAs, or uploaded Documents. Remove them before deleting.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            if (asset != null)
            {
                AddAuditLog(asset.Id, "Deleted", asset.Status.ToString(), null, asset.Name);
                asset.IsDeleted = true;
                asset.DeletedBy = _userManager.GetUserName(User);
                asset.DeletedOn = DateTime.UtcNow;
                _context.Asset.Update(asset);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Assets/Documents/5
        public async Task<IActionResult> Documents(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset
                .Include(a => a.Documents)
                .Include(a => a.Process)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (asset == null)
            {
                return NotFound();
            }

            // Recalculate document statuses
            if (asset.Documents != null)
            {
                bool changed = false;
                foreach (var doc in asset.Documents)
                {
                    AssetDocumentStatus newStatus;
                    if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow)
                        newStatus = AssetDocumentStatus.Expired;
                    else if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow.AddDays(doc.ReminderDays))
                        newStatus = AssetDocumentStatus.Expiring;
                    else
                        newStatus = AssetDocumentStatus.Valid;

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
            ViewBag.DeletedFiles = await _context.AssetDocument.DeletedOnly().Where(f => f.AssetId == id).ToListAsync();
            return View(asset);
        }

        // POST: Assets/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> UploadDocument(int assetId, IFormFile file, string? description, DateTime? issueDate, DateTime? expiryDate)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Documents), new { id = assetId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.";
                return RedirectToAction(nameof(Documents), new { id = assetId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds the 25 MB maximum size limit.";
                return RedirectToAction(nameof(Documents), new { id = assetId });
            }

            var asset = await _context.Asset.FindAsync(assetId);
            if (asset == null)
            {
                return NotFound();
            }

            if (asset.Status != AssetStatus.Draft && asset.Status != AssetStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be uploaded to Draft or Under Review assets.";
                return RedirectToAction(nameof(Documents), new { id = assetId });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "assets", assetId.ToString());
            Directory.CreateDirectory(uploadDir);

            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var doc = new AssetDocument
            {
                AssetId = assetId,
                FileName = file.FileName,
                FilePath = $"/uploads/assets/{assetId}/{uniqueName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = description,
                UploadedBy = _userManager.GetUserName(User),
                UploadedDate = DateTime.UtcNow,
                DocumentStatus = AssetDocumentStatus.Valid
            };

            _context.AssetDocument.Add(doc);
            AddAuditLog(assetId, "Document uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(Documents), new { id = assetId });
        }

        // GET: Assets/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.AssetDocument.ForDownload(deleted)
                .Include(d => d.Asset)
                .FirstOrDefaultAsync(d => d.Id == id);
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
                AddAuditLog(doc.AssetId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction("Documents", new { id = doc.AssetId });
            }

            string recordId = doc.Asset?.AssetId ?? doc.AssetId.ToString();
            string? version = doc.Asset?.Version.ToString("0.##");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Asset",
                recordId, version,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        // POST: Assets/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> DeleteDocument(int id, int assetId, string? deletionReason)
        {
            var doc = await _context.AssetDocument.FindAsync(id);
            if (doc == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset.FindAsync(doc.AssetId);
            if (asset == null)
            {
                return NotFound();
            }

            if (asset.Status != AssetStatus.Draft && asset.Status != AssetStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be deleted from Draft or Under Review assets.";
                return RedirectToAction(nameof(Documents), new { id = doc.AssetId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                return RedirectToAction(nameof(Documents), new { id = doc.AssetId });
            }

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();
            AddAuditLog(doc.AssetId, "Document deleted", null, null, $"{doc.FileName} — Reason: {doc.DeletionReason}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Documents), new { id = doc.AssetId });
        }

        public ActionResult GetDocuments()
        {
            var assets = _context.Asset.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(AssetStatus)).Cast<AssetStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            if (assets != null && assets.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = assets.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public ActionResult GetDocumentsForBarchart()
        {
            var assets = _context.Asset.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();
            var dates = assets.Select(x => x.EditedOn.ToSite().Date).Distinct().ToList();

            if (assets != null && assets.Count > 0)
            {
                foreach (var date in dates)
                {
                    labels.Add(date.ToShortDateString());
                    var assetCount = assets.Where(x => x.EditedOn.ToSite().Date == date).Count();
                    dataSet.Data.Add(assetCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public async Task<IActionResult> ExportAssetDetail(int id)
        {
            var asset = await _context.Asset
                .Where(x => x.Id == id)
                .Include(a => a.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(a => a.Changes)
                .Include(a => a.Deviations)
                .Include(a => a.Capas)
                .FirstOrDefaultAsync();

            if (asset == null)
            {
                return NotFound();
            }

            var changeList    = asset.Changes?.ToList()    ?? new List<Change>();
            var deviationList = asset.Deviations?.ToList() ?? new List<Deviation>();
            var capaList      = asset.Capas?.ToList()      ?? new List<Capa>();

            var fields = new List<(string, string?)>
            {
                ("Asset ID",    asset.AssetId),
                ("Name",        asset.Name),
                ("Status",      asset.Status.ToString()),
                ("Category",    asset.Category.HasValue
                                ? asset.Category.Value.GetType()
                                    .GetField(asset.Category.Value.ToString())
                                    ?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                                    .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>()
                                    .FirstOrDefault()?.Name ?? asset.Category.Value.ToString()
                                : null),
                ("Description", asset.Description),
                ("Process",     asset.LinkedProcesses?.Any() == true
                                ? string.Join(", ", asset.LinkedProcesses.Select(x => x.Process?.Name ?? ""))
                                : asset.Process?.Name),
                ("Edited By",   asset.EditedBy),
                ("Edited On",   asset.EditedOn.ToSite().ToShortDateString()),
                ("Approved By", asset.ApprovedBy),
                ("Approved On", asset.ApprovedOn.ToSite()?.ToShortDateString()),
            };

            var tables = new List<(string, string[], IEnumerable<string[]>)>
            {
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
            var result = PdfReportService.Generate("Asset Detail Report", asset.AssetId, asset.Status.ToString(), fields, tables,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Asset-{asset.AssetId}.pdf");
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            Asset? asset = _context.Asset.Where(x => x.Id == id).Include(x => x.LinkedProcesses).ThenInclude(x => x.Process).FirstOrDefault();

            if (asset == null)
            {
                return NotFound();
            }

            var builder = new StringBuilder();
            builder.AppendLine("Asset Id,Name,Description,Category,Process,Version,Status,Edited By,Edited On,Approved By,Approved On");
            var assetProcessNames = asset.LinkedProcesses?.Any() == true
                ? string.Join("|", asset.LinkedProcesses.Select(x => x.Process?.Name ?? ""))
                : (asset.Process?.Name ?? string.Empty);
            builder.AppendLine($"{asset.AssetId},{asset.Name},{asset.Description},{asset.Category?.ToString() ?? ""},{assetProcessNames},{asset.Version},{asset.Status},{(asset.EditedBy != null ? asset.EditedBy : String.Empty)},{asset.EditedOn.ToSite().ToShortDateString()},{(asset.ApprovedBy != null ? asset.ApprovedBy : String.Empty)},{(asset.ApprovedOn != null ? asset.ApprovedOn.Value.ToSite().ToShortDateString() : String.Empty)}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Asset.csv");
        }

        private bool AssetExists(int id)
        {
            return (_context.Asset?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MasterDataAuditLog.Add(new MasterDataAuditLog
            {
                EntityType = "Asset",
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
