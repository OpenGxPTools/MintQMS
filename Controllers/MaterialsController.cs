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
using static OpenQMS.Models.Material;
using static OpenQMS.Models.Process;
using static OpenQMS.Models.Product;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class MaterialsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        public MaterialsController
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

        private async Task SyncMaterialJunctions(int materialId, int[] productIds, int[] processIds)
        {
            _context.MaterialProduct.RemoveRange(await _context.MaterialProduct.Where(x => x.MaterialId == materialId).ToListAsync());
            _context.MaterialProcess.RemoveRange(await _context.MaterialProcess.Where(x => x.MaterialId == materialId).ToListAsync());
            foreach (var id in productIds.Distinct())
                _context.MaterialProduct.Add(new MaterialProduct { MaterialId = materialId, ProductId = id });
            foreach (var id in processIds.Distinct())
                _context.MaterialProcess.Add(new MaterialProcess { MaterialId = materialId, ProcessId = id });
        }

        private async Task SyncMaterialRelatedDocs(int materialId, int[] docIds)
        {
            _context.MaterialLinkedDocument.RemoveRange(
                await _context.MaterialLinkedDocument.Where(x => x.MaterialId == materialId).ToListAsync());
            foreach (var docId in docIds.Distinct())
            {
                var doc = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null)
                    _context.MaterialLinkedDocument.Add(new MaterialLinkedDocument
                        { MaterialId = materialId, AppDocumentId = docId, LinkedVersion = doc.Version });
            }
        }

        private void PopulateMaterialRelatedDocsViewData(int[]? selectedIds = null)
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

        // GET: Materials
        public async Task<IActionResult> Index()
        {
            var materialsList = await _context.Material.Where(m => !m.IsDeleted).Include(c => c.LinkedProducts).ThenInclude(x => x.Product).Include(c => c.LinkedProcesses).ThenInclude(x => x.Process).ToListAsync();
            return View(materialsList);
        }

        // GET: Materials/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Material == null)
            {
                return NotFound();
            }

            var material = await _context.Material
                .Include(a => a.LinkedProducts).ThenInclude(x => x.Product)
                .Include(a => a.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(a => a.Changes)
                .Include(a => a.Deviations)
                .Include(a => a.Capas)
                .Include(a => a.Documents)
                .Include(a => a.RelatedDocuments!).ThenInclude(rd => rd.Document)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
            {
                return NotFound();
            }

            ViewBag.AuditLogs = await _context.MasterDataAuditLog
                .Where(l => l.EntityType == "Material" && l.ParentId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            return View(material);
        }

        // POST: Materials/Details/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, string InputEmail, string InputPassword)
        {
            var material = _context.Material.FirstOrDefault(x => x.Id == id);

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

                if (material.Status != Material.MaterialStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only materials under review can be approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                material.Version = Decimal.ToInt32(material.Version) + 1;
                material.ApprovedBy = _userManager.GetUserName(User);
                material.ApprovedOn = DateTime.UtcNow;
                material.Status = Material.MaterialStatus.Approved;
                material.ExportFilePath = String.Empty;

                if (material.GeneratedFrom != null && !string.IsNullOrEmpty(material.GeneratedFrom))
                {
                    var oldMaterial = await _context.Material.FirstOrDefaultAsync(m => m.Id.ToString().Equals(material.GeneratedFrom));
                    if (oldMaterial != null)
                    {
                        oldMaterial.IsLocked = false;
                        oldMaterial.Status = Material.MaterialStatus.Obsolete;
                        oldMaterial.ExportFilePath = String.Empty;
                        _context.Material.Update(oldMaterial);
                    }
                }

                _context.Material.Update(material);
                AddAuditLog(material.Id, "Approved", "UnderReview", "Approved");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Material approved successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MaterialExists(material.Id))
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

        // POST: Materials/SubmitForReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> SubmitForReview(int id)
        {
            var material = await _context.Material.FirstOrDefaultAsync(x => x.Id == id);
            if (material == null) return NotFound();

            if (material.Status != Material.MaterialStatus.Draft)
            {
                TempData["WorkflowError"] = "Only Draft materials can be submitted for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            material.Status = Material.MaterialStatus.UnderReview;
            _context.Material.Update(material);
            AddAuditLog(material.Id, "Submitted for review", "Draft", "UnderReview");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Material submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Materials/SendBackToDraft/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBackToDraft(int id)
        {
            var material = await _context.Material.FirstOrDefaultAsync(x => x.Id == id);
            if (material == null) return NotFound();

            var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.AuthorisedPersonRole);
            if (!isAuthorized)
            {
                TempData["WorkflowError"] = "You do not have permission to send back for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (material.Status != Material.MaterialStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only materials under review can be sent back to draft.";
                return RedirectToAction(nameof(Details), new { id });
            }

            material.Status = Material.MaterialStatus.Draft;
            _context.Material.Update(material);
            AddAuditLog(material.Id, "Sent back to draft", "UnderReview", "Draft");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Material sent back to Draft for rework.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private void PopulateMaterialViewData()
        {
            ViewData["Products"] = _context.Product
                .Where(x => !x.IsDeleted && x.Status.Equals(ProductStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
            ViewData["Processes"] = _context.Process
                .Where(x => !x.IsDeleted && x.Status.Equals(ProcessStatus.Approved))
                .OrderBy(x => x.Name)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToList();
        }

        // GET: Materials/Create
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public IActionResult Create()
        {
            PopulateMaterialViewData();
            ViewBag.SelectedProductIds = new List<int>();
            ViewBag.SelectedProcessIds = new List<int>();
            PopulateMaterialRelatedDocsViewData();
            return View();
        }

        // POST: Materials/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Create([Bind("MaterialId,Name,MaterialCategory,Description")] Material material, int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedRelatedDocs)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var prevId = _context.Material
                        .AsEnumerable()
                        .Where(x => !string.IsNullOrEmpty(x.MaterialId))
                        .Select(x => {
                            var parts = x.MaterialId.Split('-');
                            return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    material.MaterialId = $"MAT-{(prevId + 1).ToString().PadLeft(4, '0')}";
                    material.EditedBy = _userManager.GetUserName(User);
                    material.EditedOn = DateTime.UtcNow;
                    material.Version = Convert.ToDecimal(0.01);
                    material.IsLocked = false;

                    _context.Add(material);
                    await _context.SaveChangesAsync();

                    await SyncMaterialJunctions(material.Id, selectedProducts ?? Array.Empty<int>(), selectedProcesses ?? Array.Empty<int>());
                    await SyncMaterialRelatedDocs(material.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    AddAuditLog(material.Id, "Created", null, "Draft",
                        $"ID: {material.MaterialId}; Name: {material.Name}");
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id = material.Id });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            PopulateMaterialViewData();
            ViewBag.SelectedProductIds = selectedProducts?.ToList() ?? new List<int>();
            ViewBag.SelectedProcessIds = selectedProcesses?.ToList() ?? new List<int>();
            PopulateMaterialRelatedDocsViewData(selectedRelatedDocs);
            return View(material);
        }

        // GET: Materials/Edit/5
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Material == null)
            {
                return NotFound();
            }

            var material = await _context.Material
                .Include(m => m.LinkedProducts).ThenInclude(x => x.Product)
                .Include(m => m.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(m => m.RelatedDocuments!)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (material == null)
            {
                return NotFound();
            }

            if (material.IsLocked || material.Status == Material.MaterialStatus.Obsolete)
            {
                return Forbid();
            }

            if (material.Status == Material.MaterialStatus.UnderReview)
            {
                TempData["Error"] = "This material is currently under review and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            PopulateMaterialViewData();
            ViewBag.SelectedProductIds = material.LinkedProducts?.Any() == true
                ? material.LinkedProducts.Select(x => x.ProductId).ToList()
                : new List<int>();
            ViewBag.SelectedProcessIds = material.LinkedProcesses?.Any() == true
                ? material.LinkedProcesses.Select(x => x.ProcessId).ToList()
                : (material.ProcessId.HasValue ? new List<int> { material.ProcessId.Value } : new List<int>());
            var relDocIds = material.RelatedDocuments?.Select(rd => rd.AppDocumentId).ToArray();
            PopulateMaterialRelatedDocsViewData(relDocIds);
            return View(material);
        }

        // POST: Materials/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MaterialId,Name,MaterialCategory,Description,Version,EditedBy,EditedOn,ApprovedBy,ApprovedOn,GeneratedFrom,IsLocked,Status")] Material material, int[]? selectedProducts, int[]? selectedProcesses, int[]? selectedRelatedDocs)
        {
            if (id != material.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Lock/status decisions and system-managed fields come from the stored record,
                    // never from the posted (hidden) form values.
                    var current = await _context.Material.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    if (current == null) return NotFound();
                    if (current.IsLocked || current.Status == Material.MaterialStatus.Obsolete) return Forbid();
                    if (current.Status == Material.MaterialStatus.UnderReview)
                    {
                        TempData["Error"] = "This material is currently under review and cannot be edited.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    material.IsLocked      = current.IsLocked;
                    material.Status        = current.Status;
                    material.Version       = current.Version;
                    material.ApprovedBy    = current.ApprovedBy;
                    material.ApprovedOn    = current.ApprovedOn;
                    material.GeneratedFrom = current.GeneratedFrom;
                    material.MaterialId = current.MaterialId;
                    material.IsDeleted     = current.IsDeleted;
                    material.DeletedBy     = current.DeletedBy;
                    material.DeletedOn     = current.DeletedOn;

                    // Field-level change tracking for the audit log
                    var snap = await _context.Material.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff, "Name",     snap.Name,             material.Name);
                        AuditDiffHelper.Track(diff, "Category", snap.MaterialCategory, material.MaterialCategory);
                        AuditDiffHelper.Track(diff, "Description", snap.Description,    material.Description);
                    }
                    // Linked-record changes (queried before the sync below, so these are the old associations)
                    var oldMtProductIds = await _context.MaterialProduct.Where(x => x.MaterialId == id).Select(x => x.ProductId).ToListAsync();
                    var oldMtProcessIds = await _context.MaterialProcess.Where(x => x.MaterialId == id).Select(x => x.ProcessId).ToListAsync();
                    var oldMtDocIds     = await _context.MaterialLinkedDocument.Where(x => x.MaterialId == id).Select(x => x.AppDocumentId).ToListAsync();
                    AuditDiffHelper.TrackSet(diff, "Linked Products",  oldMtProductIds, selectedProducts   ?? Array.Empty<int>(),
                        ids => _context.Product.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                    AuditDiffHelper.TrackSet(diff, "Linked Processes", oldMtProcessIds, selectedProcesses  ?? Array.Empty<int>(),
                        ids => _context.Process.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name));
                    AuditDiffHelper.TrackSet(diff, "Linked Documents", oldMtDocIds,     selectedRelatedDocs ?? Array.Empty<int>(),
                        ids => _context.AppDocument.Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id, d => d.DocumentId));
                    var editNotes = AuditDiffHelper.Build(diff);

                    material.Version += Convert.ToDecimal(0.01);
                    material.EditedBy = _userManager.GetUserName(User);
                    material.EditedOn = DateTime.UtcNow;
                    material.ExportFilePath = string.Empty;

                    if (material.Status == MaterialStatus.Approved)
                    {
                        if (material.IsLocked || material.Status == Material.MaterialStatus.Obsolete)
                        {
                            return Forbid();
                        }
                        using (var transaction = _context.Database.BeginTransaction())
                        {
                            material.Id = _context.Material.Max(m => m.Id) + 1;
                            material.Status = MaterialStatus.Draft;
                            material.ApprovedBy = null;
                            material.ApprovedOn = null;
                            material.GeneratedFrom = id.ToString();
                            material.IsLocked = false;
                            _context.Add(material);
                            _context.SaveChanges();
                            transaction.Commit();
                        }

                        Material? material1 = await _context.Material.FirstOrDefaultAsync(m => m.Id == id);
                        material1.IsLocked = true;
                        _context.Update(material1);
                        await _context.SaveChangesAsync();
                        await SyncMaterialJunctions(material.Id, selectedProducts ?? Array.Empty<int>(), selectedProcesses ?? Array.Empty<int>());
                        await SyncMaterialRelatedDocs(material.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        AddAuditLog(material.Id, "Edited (new revision)", "Approved", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        material.Status = MaterialStatus.Draft;
                        _context.Update(material);
                        await SyncMaterialJunctions(material.Id, selectedProducts ?? Array.Empty<int>(), selectedProcesses ?? Array.Empty<int>());
                        await SyncMaterialRelatedDocs(material.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        if (diff.Count > 0) AddAuditLog(material.Id, "Edited", "Draft", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MaterialExists(material.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Details), new { id = material.Id });
            }

            PopulateMaterialViewData();
            ViewBag.SelectedProductIds = selectedProducts?.ToList() ?? new List<int>();
            ViewBag.SelectedProcessIds = selectedProcesses?.ToList() ?? new List<int>();
            PopulateMaterialRelatedDocsViewData(selectedRelatedDocs);
            return View(material);
        }

        // GET: Materials/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Material == null)
            {
                return NotFound();
            }

            var material = await _context.Material
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
            {
                return NotFound();
            }

            if (material.Status != MaterialStatus.Draft)
            {
                TempData["Error"] = "Only Draft materials can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["HasLinkedRecords"] = material.Changes?.Any() == true
                || material.Deviations?.Any() == true
                || material.Capas?.Any() == true
                || material.Documents?.Any() == true;

            return View(material);
        }

        // POST: Materials/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Material == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Material' is null.");
            }

            var material = await _context.Material
                .Include(c => c.LinkedProducts).ThenInclude(x => x.Product)
                .Include(c => c.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material.Status != MaterialStatus.Draft)
            {
                TempData["Error"] = "Only Draft materials can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (material.Changes?.Any() == true || material.Deviations?.Any() == true
                || material.Capas?.Any() == true || material.Documents?.Any() == true)
            {
                TempData["ErrorMessage"] = "This material has linked Changes, Deviations, CAPAs, or uploaded Documents. Remove them before deleting.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            if (material != null)
            {
                AddAuditLog(material.Id, "Deleted", material.Status.ToString(), null, material.Name);
                material.IsDeleted = true;
                material.DeletedBy = _userManager.GetUserName(User);
                material.DeletedOn = DateTime.UtcNow;
                _context.Material.Update(material);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Materials/Documents/5
        public async Task<IActionResult> Documents(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var material = await _context.Material
                .Include(m => m.Documents)
                .Include(m => m.Process)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
            {
                return NotFound();
            }

            // Recalculate document statuses
            if (material.Documents != null)
            {
                bool changed = false;
                foreach (var doc in material.Documents)
                {
                    MaterialDocumentStatus newStatus;
                    if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow)
                        newStatus = MaterialDocumentStatus.Expired;
                    else if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow.AddDays(doc.ReminderDays))
                        newStatus = MaterialDocumentStatus.Expiring;
                    else
                        newStatus = MaterialDocumentStatus.Valid;

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
            ViewBag.DeletedFiles = await _context.MaterialDocument.DeletedOnly().Where(f => f.MaterialId == id).ToListAsync();
            return View(material);
        }

        // POST: Materials/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> UploadDocument(int materialId, IFormFile file, string? description, DateTime? issueDate, DateTime? expiryDate)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Documents), new { id = materialId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.";
                return RedirectToAction(nameof(Documents), new { id = materialId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds the 25 MB maximum size limit.";
                return RedirectToAction(nameof(Documents), new { id = materialId });
            }

            var material = await _context.Material.FindAsync(materialId);
            if (material == null)
            {
                return NotFound();
            }

            if (material.Status != MaterialStatus.Draft && material.Status != MaterialStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be uploaded to Draft or Under Review materials.";
                return RedirectToAction(nameof(Documents), new { id = materialId });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "materials", materialId.ToString());
            Directory.CreateDirectory(uploadDir);

            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var doc = new MaterialDocument
            {
                MaterialId = materialId,
                FileName = file.FileName,
                FilePath = $"/uploads/materials/{materialId}/{uniqueName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = description,
                UploadedBy = _userManager.GetUserName(User),
                UploadedDate = DateTime.UtcNow,
                DocumentStatus = MaterialDocumentStatus.Valid
            };

            _context.MaterialDocument.Add(doc);
            AddAuditLog(materialId, "Document uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(Documents), new { id = materialId });
        }

        // GET: Materials/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.MaterialDocument.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
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
                AddAuditLog(doc.MaterialId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction("Documents", new { id = doc.MaterialId });
            }

            var parent  = await _context.Material.FindAsync(doc.MaterialId);
            var version = parent?.Version.ToString("0.##");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Material",
                doc.MaterialId.ToString(), version,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        // POST: Materials/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> DeleteDocument(int id, int materialId, string? deletionReason)
        {
            var doc = await _context.MaterialDocument.FindAsync(id);
            if (doc == null)
            {
                return NotFound();
            }

            var material = await _context.Material.FindAsync(doc.MaterialId);
            if (material == null)
            {
                return NotFound();
            }

            if (material.Status != MaterialStatus.Draft && material.Status != MaterialStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be deleted from Draft or Under Review materials.";
                return RedirectToAction(nameof(Documents), new { id = doc.MaterialId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                return RedirectToAction(nameof(Documents), new { id = doc.MaterialId });
            }

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();
            AddAuditLog(doc.MaterialId, "Document deleted", null, null, $"{doc.FileName} — Reason: {doc.DeletionReason}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Documents), new { id = doc.MaterialId });
        }

        public ActionResult GetDocuments()
        {
            var materials = _context.Material.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(MaterialStatus)).Cast<MaterialStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            if (materials != null && materials.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = materials.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public ActionResult GetDocumentsForBarchart()
        {
            var materials = _context.Material.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();
            var dates = materials.Select(x => x.EditedOn.ToSite().Date).Distinct().ToList();

            if (materials != null && materials.Count > 0)
            {
                foreach (var date in dates)
                {
                    labels.Add(date.ToShortDateString());
                    var materialCount = materials.Where(x => x.EditedOn.ToSite().Date == date).Count();
                    dataSet.Data.Add(materialCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public async Task<IActionResult> ExportMaterialDetail(int id)
        {
            var material = await _context.Material
                .Where(x => x.Id == id)
                .Include(a => a.LinkedProducts).ThenInclude(x => x.Product)
                .Include(a => a.LinkedProcesses).ThenInclude(x => x.Process)
                .Include(a => a.Changes)
                .Include(a => a.Deviations)
                .Include(a => a.Capas)
                .FirstOrDefaultAsync();

            if (material == null)
            {
                return NotFound();
            }

            var changeList    = material.Changes?.ToList()    ?? new List<Change>();
            var deviationList = material.Deviations?.ToList() ?? new List<Deviation>();
            var capaList      = material.Capas?.ToList()      ?? new List<Capa>();

            var fields = new List<(string, string?)>
            {
                ("Material ID",      material.MaterialId),
                ("Name",             material.Name),
                ("Type / Category",  material.MaterialCategory),
                ("Status",           material.Status.ToString()),
                ("Description",      material.Description),
                ("Products",    material.LinkedProducts?.Any() == true
                                ? string.Join(", ", material.LinkedProducts.Select(x => x.Product?.Name ?? ""))
                                : "-"),
                ("Process",     material.LinkedProcesses?.Any() == true
                                ? string.Join(", ", material.LinkedProcesses.Select(x => x.Process?.Name ?? ""))
                                : material.Process?.Name),
                ("Edited By",   material.EditedBy),
                ("Edited On",   material.EditedOn.ToSite().ToShortDateString()),
                ("Approved By", material.ApprovedBy),
                ("Approved On", material.ApprovedOn.ToSite()?.ToShortDateString()),
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
            var result = PdfReportService.Generate("Material Detail Report", material.MaterialId, material.Status.ToString(), fields, tables,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Material-{material.MaterialId}.pdf");
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            Material? material = _context.Material.Where(x => x.Id == id).Include(x => x.LinkedProducts).ThenInclude(x => x.Product).Include(x => x.LinkedProcesses).ThenInclude(x => x.Process).FirstOrDefault();

            if (material == null)
            {
                return NotFound();
            }

            var materialProductNames = material.LinkedProducts?.Any() == true
                ? string.Join("|", material.LinkedProducts.Select(x => x.Product?.Name ?? ""))
                : string.Empty;
            var materialProcessNames = material.LinkedProcesses?.Any() == true
                ? string.Join("|", material.LinkedProcesses.Select(x => x.Process?.Name ?? ""))
                : (material.Process?.Name ?? string.Empty);
            var builder = new StringBuilder();
            builder.AppendLine("Material Id,Name,Type / Category,Description,Products,Process,Version,Status,Edited By,Edited On,Approved By,Approved On");
            builder.AppendLine($"{material.MaterialId},{material.Name},{material.MaterialCategory ?? string.Empty},{material.Description},{materialProductNames},{materialProcessNames},{material.Version},{material.Status},{(material.EditedBy != null ? material.EditedBy : String.Empty)},{material.EditedOn.ToSite().ToShortDateString()},{(material.ApprovedBy != null ? material.ApprovedBy : String.Empty)},{(material.ApprovedOn != null ? material.ApprovedOn.Value.ToSite().ToShortDateString() : String.Empty)}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Material.csv");
        }

        private bool MaterialExists(int id)
        {
            return (_context.Material?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MasterDataAuditLog.Add(new MasterDataAuditLog
            {
                EntityType = "Material",
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
