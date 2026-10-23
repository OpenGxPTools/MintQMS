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
using static OpenQMS.Models.Product;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        public ProductsController
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

        private async Task SyncProductRelatedDocs(int productId, int[] docIds)
        {
            _context.ProductLinkedDocument.RemoveRange(
                await _context.ProductLinkedDocument.Where(x => x.ProductId == productId).ToListAsync());
            foreach (var docId in docIds.Distinct())
            {
                var doc = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null)
                    _context.ProductLinkedDocument.Add(new ProductLinkedDocument
                        { ProductId = productId, AppDocumentId = docId, LinkedVersion = doc.Version });
            }
        }

        private void PopulateProductRelatedDocsViewData(int[]? selectedIds = null)
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

        // GET: Products
        public async Task<IActionResult> Index()
        {
            var productsList = await _context.Product.Where(p => !p.IsDeleted).ToListAsync();
            return View(productsList);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Product == null)
            {
                return NotFound();
            }

            var product = await _context.Product
                .Include(a => a.Changes)
                .Include(a => a.Deviations)
                .Include(a => a.Capas)
                .Include(a => a.LinkedComplaints).ThenInclude(cp => cp.Complaint)
                .Include(a => a.Documents)
                .Include(a => a.Strengths)
                .Include(a => a.Specifications)
                .Include(a => a.RelatedDocuments!).ThenInclude(rd => rd.Document)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            ViewData["AECount"] = await _context.AdverseEvent
                .CountAsync(a => a.SuspectProduct != null && a.SuspectProduct.ProductId == id);

            ViewBag.AuditLogs = await _context.MasterDataAuditLog
                .Where(l => l.EntityType == "Product" && l.ParentId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            return View(product);
        }

        // POST: Products/Details/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, string InputEmail, string InputPassword)
        {
            var product = _context.Product.FirstOrDefault(x => x.Id == id);

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

                if (product.Status != ProductStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only products under review can be approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                product.Version = Decimal.ToInt32(product.Version) + 1;
                product.ApprovedBy = _userManager.GetUserName(User);
                product.ApprovedOn = DateTime.UtcNow;
                product.Status = ProductStatus.Approved;
                product.ExportFilePath = String.Empty;

                if (product.GeneratedFrom != null && !string.IsNullOrEmpty(product.GeneratedFrom))
                {
                    var oldProduct = await _context.Product.FirstOrDefaultAsync(m => m.Id.ToString().Equals(product.GeneratedFrom));
                    if (oldProduct != null)
                    {
                        oldProduct.IsLocked = false;
                        oldProduct.Status = ProductStatus.Obsolete;
                        oldProduct.ExportFilePath = String.Empty;
                        _context.Product.Update(oldProduct);
                    }
                }

                _context.Product.Update(product);
                AddAuditLog(product.Id, "Approved", ProductStatus.UnderReview.ToString(), ProductStatus.Approved.ToString());
                await _context.SaveChangesAsync();
                TempData["Success"] = "Product approved successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.Id))
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

        // POST: Products/SubmitForReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(int id)
        {
            var product = await _context.Product.FirstOrDefaultAsync(x => x.Id == id);
            if (product == null) return NotFound();

            if (product.Status != ProductStatus.Draft)
            {
                TempData["WorkflowError"] = "Only Draft products can be submitted for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            product.Status = ProductStatus.UnderReview;
            _context.Product.Update(product);
            AddAuditLog(product.Id, "Submitted for review", ProductStatus.Draft.ToString(), ProductStatus.UnderReview.ToString());
            await _context.SaveChangesAsync();
            TempData["Success"] = "Product submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Products/SendBackToDraft/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBackToDraft(int id)
        {
            var product = await _context.Product.FirstOrDefaultAsync(x => x.Id == id);
            if (product == null) return NotFound();

            var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole) || User.IsInRole(Constants.AuthorisedPersonRole);
            if (!isAuthorized)
            {
                TempData["WorkflowError"] = "You do not have permission to send back for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (product.Status != ProductStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only products under review can be sent back to draft.";
                return RedirectToAction(nameof(Details), new { id });
            }

            product.Status = ProductStatus.Draft;
            _context.Product.Update(product);
            AddAuditLog(product.Id, "Sent back to draft", ProductStatus.UnderReview.ToString(), ProductStatus.Draft.ToString());
            await _context.SaveChangesAsync();
            TempData["Success"] = "Product sent back to Draft for rework.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Products/Create
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public IActionResult Create()
        {
            PopulateProductRelatedDocsViewData();
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Create([Bind("ProductId,Name,ProductType,InnName,BrandName,DosageForm,ArtgReference,Description,InciName,PresentationForm,DeviceTypeCategory,ModelVersion,StandardScientificName,RawMaterialTypeCategory,Grade")] Product product, int[]? selectedRelatedDocs)
        {
            try
            {
                if(ModelState.IsValid)
                {
                    var prevId = _context.Product
                        .AsEnumerable()
                        .Where(x => !string.IsNullOrEmpty(x.ProductId))
                        .Select(x => {
                            var parts = x.ProductId.Split('-');
                            return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    product.ProductId = $"PRD-{(prevId + 1).ToString().PadLeft(4, '0')}";
                    product.EditedBy = _userManager.GetUserName(User);
                    product.EditedOn = DateTime.UtcNow;
                    product.Version = Convert.ToDecimal(0.01);
                    product.IsLocked = false;

                    _context.Add(product);
                    await _context.SaveChangesAsync();
                    await SyncProductRelatedDocs(product.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    AddAuditLog(product.Id, "Created", null, ProductStatus.Draft.ToString(),
                        $"ID: {product.ProductId}; Name: {product.Name}");
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id = product.Id });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            PopulateProductRelatedDocsViewData(selectedRelatedDocs);
            return View(product);
        }

        // GET: Products/Edit/5
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Product == null)
            {
                return NotFound();
            }

            var product = await _context.Product
                .Include(p => p.Strengths)
                .Include(p => p.RelatedDocuments!)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            if (product.IsLocked || product.Status == ProductStatus.Obsolete)
            {
                return Forbid();
            }

            if (product.Status == ProductStatus.UnderReview)
            {
                TempData["Error"] = "This product is currently under review and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var relDocIds = product.RelatedDocuments?.Select(rd => rd.AppDocumentId).ToArray();
            PopulateProductRelatedDocsViewData(relDocIds);
            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProductId,GeneratedFrom,Name,ProductType,InnName,BrandName,DosageForm,ArtgReference,Description,InciName,PresentationForm,DeviceTypeCategory,ModelVersion,StandardScientificName,RawMaterialTypeCategory,Grade,Version,EditedBy,EditedOn,ApprovedBy,ApprovedOn,IsLocked,Status")] Product product, int[]? selectedRelatedDocs)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Lock/status decisions and system-managed fields come from the stored record,
                    // never from the posted (hidden) form values.
                    var current = await _context.Product.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    if (current == null) return NotFound();
                    if (current.IsLocked || current.Status == ProductStatus.Obsolete) return Forbid();
                    if (current.Status == ProductStatus.UnderReview)
                    {
                        TempData["Error"] = "This product is currently under review and cannot be edited.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    product.IsLocked      = current.IsLocked;
                    product.Status        = current.Status;
                    product.Version       = current.Version;
                    product.ApprovedBy    = current.ApprovedBy;
                    product.ApprovedOn    = current.ApprovedOn;
                    product.GeneratedFrom = current.GeneratedFrom;
                    product.ProductId = current.ProductId;
                    product.IsDeleted     = current.IsDeleted;
                    product.DeletedBy     = current.DeletedBy;
                    product.DeletedOn     = current.DeletedOn;

                    // Field-level change tracking for the audit log
                    var snap = await _context.Product.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff,     "Name",                     snap.Name,                    product.Name);
                        AuditDiffHelper.TrackEnum(diff, "Product Type",             snap.ProductType,             product.ProductType);
                        AuditDiffHelper.Track(diff,     "INN Name",                 snap.InnName,                 product.InnName);
                        AuditDiffHelper.Track(diff,     "Brand Name",               snap.BrandName,               product.BrandName);
                        AuditDiffHelper.Track(diff,     "Dosage Form",              snap.DosageForm,              product.DosageForm);
                        AuditDiffHelper.Track(diff,     "ARTG Reference",           snap.ArtgReference,           product.ArtgReference);
                        AuditDiffHelper.Track(diff,     "Description",              snap.Description,             product.Description);
                        AuditDiffHelper.Track(diff,     "INCI Name",                snap.InciName,                product.InciName);
                        AuditDiffHelper.Track(diff,     "Presentation Form",        snap.PresentationForm,        product.PresentationForm);
                        AuditDiffHelper.Track(diff,     "Device Type / Category",   snap.DeviceTypeCategory,      product.DeviceTypeCategory);
                        AuditDiffHelper.Track(diff,     "Model / Version",          snap.ModelVersion,            product.ModelVersion);
                        AuditDiffHelper.Track(diff,     "Scientific Name",          snap.StandardScientificName,  product.StandardScientificName);
                        AuditDiffHelper.Track(diff,     "Raw Material Type / Cat.", snap.RawMaterialTypeCategory, product.RawMaterialTypeCategory);
                        AuditDiffHelper.Track(diff,     "Grade",                    snap.Grade,                   product.Grade);
                    }
                    var editNotes = AuditDiffHelper.Build(diff);

                    product.Version += Convert.ToDecimal(0.01);
                    product.EditedBy = _userManager.GetUserName(User);
                    product.EditedOn = DateTime.UtcNow;
                    product.ExportFilePath = string.Empty;

                    if (product.Status == ProductStatus.Approved)
                    {
                        using (var transaction = _context.Database.BeginTransaction())
                        {
                            product.Id = _context.Product.Max(m => m.Id) + 1;
                            product.Status = ProductStatus.Draft;
                            product.ApprovedBy = null;
                            product.ApprovedOn = null;
                            product.GeneratedFrom = id.ToString();
                            product.IsLocked = false;
                            _context.Add(product);
                            _context.SaveChanges();
                            transaction.Commit();
                        }

                        Product? product1 = await _context.Product.FirstOrDefaultAsync(m => m.Id == id);
                        product1.IsLocked = true;
                        _context.Update(product1);
                        await _context.SaveChangesAsync();
                        AddAuditLog(product.Id, "Edited (new revision)", ProductStatus.Approved.ToString(), ProductStatus.Draft.ToString(), editNotes);
                        await _context.SaveChangesAsync();
                        await SyncProductRelatedDocs(product.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    }
                    else
                    {
                        product.Status = ProductStatus.Draft;
                        _context.Update(product);
                        if (diff.Count > 0) AddAuditLog(product.Id, "Edited", ProductStatus.Draft.ToString(), ProductStatus.Draft.ToString(), editNotes);
                        await _context.SaveChangesAsync();
                        await SyncProductRelatedDocs(product.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Details), new { id = product.Id });
            }
            PopulateProductRelatedDocsViewData(selectedRelatedDocs);
            return View(product);
        }

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Product == null)
            {
                return NotFound();
            }

            var product = await _context.Product
                .Include(p => p.Processes)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            if (product.Status != ProductStatus.Draft)
            {
                TempData["Error"] = "Only Draft products can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["HasLinkedRecords"] = product.Processes?.Any() == true
                || product.Changes?.Any() == true
                || product.Deviations?.Any() == true
                || product.Capas?.Any() == true
                || product.Documents?.Any() == true;

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Product == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Product' is null.");
            }

            var product = await _context.Product
                .Include(p => p.Processes)
                .Include(p => p.Changes)
                .Include(p => p.Deviations)
                .Include(p => p.Capas)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product.Status != ProductStatus.Draft)
            {
                TempData["Error"] = "Only Draft products can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (product.Processes?.Any() == true || product.Changes?.Any() == true
                || product.Deviations?.Any() == true || product.Capas?.Any() == true
                || product.Documents?.Any() == true)
            {
                TempData["ErrorMessage"] = "This product has linked Processes, Changes, Deviations, CAPAs, or uploaded Documents. Remove them before deleting.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            if (product != null)
            {
                AddAuditLog(product.Id, "Deleted", product.Status.ToString(), null, product.Name);
                product.IsDeleted = true;
                product.DeletedBy = _userManager.GetUserName(User);
                product.DeletedOn = DateTime.UtcNow;
                _context.Product.Update(product);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Products/Documents/5
        public async Task<IActionResult> Documents(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Product
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Recalculate document statuses
            if (product.Documents != null)
            {
                bool changed = false;
                foreach (var doc in product.Documents)
                {
                    ProductDocumentStatus newStatus;
                    if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow)
                        newStatus = ProductDocumentStatus.Expired;
                    else if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow.AddDays(doc.ReminderDays))
                        newStatus = ProductDocumentStatus.Expiring;
                    else
                        newStatus = ProductDocumentStatus.Valid;

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
            ViewBag.DeletedFiles = await _context.ProductDocument.DeletedOnly().Where(f => f.ProductId == id).ToListAsync();
            return View(product);
        }

        // POST: Products/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> UploadDocument(int productId, IFormFile file, string? description, DateTime? issueDate, DateTime? expiryDate)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Documents), new { id = productId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.";
                return RedirectToAction(nameof(Documents), new { id = productId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds the 25 MB maximum size limit.";
                return RedirectToAction(nameof(Documents), new { id = productId });
            }

            var product = await _context.Product.FindAsync(productId);
            if (product == null)
            {
                return NotFound();
            }

            if (product.Status != ProductStatus.Draft && product.Status != ProductStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be uploaded to Draft or Under Review products.";
                return RedirectToAction(nameof(Documents), new { id = productId });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products", productId.ToString());
            Directory.CreateDirectory(uploadDir);

            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var doc = new ProductDocument
            {
                ProductId = productId,
                FileName = file.FileName,
                FilePath = $"/uploads/products/{productId}/{uniqueName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = description,
                UploadedBy = _userManager.GetUserName(User),
                UploadedDate = DateTime.UtcNow,
                DocumentStatus = ProductDocumentStatus.Valid
            };

            _context.ProductDocument.Add(doc);
            AddAuditLog(productId, "Document uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(Documents), new { id = productId });
        }

        // GET: Products/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.ProductDocument.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
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
                AddAuditLog(doc.ProductId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction("Documents", new { id = doc.ProductId });
            }

            var parent  = await _context.Product.FindAsync(doc.ProductId);
            var version = parent?.Version.ToString("0.##");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Product",
                doc.ProductId.ToString(), version,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        // POST: Products/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> DeleteDocument(int id, int productId, string? deletionReason)
        {
            var doc = await _context.ProductDocument.FindAsync(id);
            if (doc == null)
            {
                return NotFound();
            }

            var product = await _context.Product.FindAsync(doc.ProductId);
            if (product == null)
            {
                return NotFound();
            }

            if (product.Status != ProductStatus.Draft && product.Status != ProductStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be deleted from Draft or Under Review products.";
                return RedirectToAction(nameof(Documents), new { id = doc.ProductId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                return RedirectToAction(nameof(Documents), new { id = doc.ProductId });
            }

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();
            AddAuditLog(doc.ProductId, "Document deleted", null, null, $"{doc.FileName} — Reason: {doc.DeletionReason}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Documents), new { id = doc.ProductId });
        }

        // POST: Products/AddStrength
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> AddStrength(int productId, string value)
        {
            var product = await _context.Product.FindAsync(productId);
            if (product == null) return NotFound();
            if (product.IsLocked || product.Status == ProductStatus.UnderReview
                || product.Status == ProductStatus.Approved || product.Status == ProductStatus.Obsolete)
            {
                TempData["Error"] = "Strengths can only be modified on a Draft product. Save the product first to create a new Draft revision, then add strengths.";
                return RedirectToAction(nameof(Edit), new { id = productId });
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                TempData["Error"] = "Strength value cannot be empty.";
                return RedirectToAction(nameof(Edit), new { id = productId });
            }

            _context.ProductStrengths.Add(new ProductStrength { ProductId = productId, Value = value.Trim() });
            AddAuditLog(productId, "Strength Added", null, null, value.Trim());
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Strength '{value.Trim()}' added.";
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        // POST: Products/RemoveStrength
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> RemoveStrength(int strengthId)
        {
            var strength = await _context.ProductStrengths
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.Id == strengthId);
            if (strength == null) return NotFound();
            if (strength.Product?.IsLocked == true || strength.Product?.Status == ProductStatus.Approved || strength.Product?.Status == ProductStatus.Obsolete)
            {
                TempData["Error"] = "Strengths can only be modified on a Draft product. Save the product first to create a new Draft revision, then add strengths.";
                return RedirectToAction(nameof(Edit), new { id = strength.ProductId });
            }

            var productId = strength.ProductId;
            AddAuditLog(productId, "Strength Removed", null, null, strength.Value);
            _context.ProductStrengths.Remove(strength);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Strength '{strength.Value}' removed.";
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        // ── Product Specifications ─────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> AddSpecLine(int productId, string testParameter, string? method,
            string acceptanceCriterion, string? comments)
        {
            var product = await _context.Product.FindAsync(productId);
            if (product == null) return NotFound();
            if (product.IsLocked || product.Status == ProductStatus.UnderReview
                || product.Status == ProductStatus.Approved || product.Status == ProductStatus.Obsolete)
            {
                TempData["Error"] = "Specification lines can only be modified on a Draft product. Save the product first to create a new Draft revision.";
                return RedirectToAction(nameof(Details), new { id = productId });
            }

            if (string.IsNullOrWhiteSpace(testParameter) || string.IsNullOrWhiteSpace(acceptanceCriterion))
            {
                TempData["Error"] = "Test Parameter and Acceptance Criterion are required.";
                TempData["ActiveTab"] = "specs";
                return RedirectToAction(nameof(Details), new { id = productId });
            }

            var maxOrder = await _context.ProductSpecLines
                .Where(s => s.ProductId == productId)
                .MaxAsync(s => (int?)s.DisplayOrder) ?? 0;

            _context.ProductSpecLines.Add(new ProductSpecLine
            {
                ProductId           = productId,
                TestParameter       = testParameter.Trim(),
                Method              = method?.Trim(),
                AcceptanceCriterion = acceptanceCriterion.Trim(),
                Comments            = comments?.Trim(),
                DisplayOrder        = maxOrder + 1,
                CreatedBy           = _userManager.GetUserName(User),
                CreatedDate         = DateTime.UtcNow
            });

            AddAuditLog(productId, "Specification Line Added", null, null, testParameter.Trim());
            await _context.SaveChangesAsync();

            TempData["Success"] = "Specification line added.";
            TempData["ActiveTab"] = "specs";
            return RedirectToAction(nameof(Details), new { id = productId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> UpdateSpecLine(int specLineId, string? testParameter,
            string? method, string? acceptanceCriterion, string? comments)
        {
            var line = await _context.ProductSpecLines
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.Id == specLineId);

            if (line == null) return NotFound();
            if (line.Product?.IsLocked == true || line.Product?.Status == ProductStatus.UnderReview
                || line.Product?.Status == ProductStatus.Approved || line.Product?.Status == ProductStatus.Obsolete)
            {
                TempData["Error"] = "Specification lines can only be modified on a Draft product.";
                return RedirectToAction(nameof(Details), new { id = line.ProductId });
            }

            if (!string.IsNullOrWhiteSpace(testParameter)) line.TestParameter = testParameter.Trim();
            if (!string.IsNullOrWhiteSpace(acceptanceCriterion)) line.AcceptanceCriterion = acceptanceCriterion.Trim();
            line.Method   = method?.Trim();
            line.Comments = comments?.Trim();

            AddAuditLog(line.ProductId, "Specification Line Updated", null, null, line.TestParameter);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> DeleteSpecLine(int specLineId)
        {
            var line = await _context.ProductSpecLines
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.Id == specLineId);

            if (line == null) return NotFound();
            if (line.Product?.IsLocked == true || line.Product?.Status == ProductStatus.UnderReview
                || line.Product?.Status == ProductStatus.Approved || line.Product?.Status == ProductStatus.Obsolete)
            {
                TempData["Error"] = "Specification lines can only be modified on a Draft product.";
                return RedirectToAction(nameof(Details), new { id = line.ProductId });
            }

            var productId = line.ProductId;
            AddAuditLog(productId, "Specification Line Deleted", null, null, line.TestParameter);
            _context.ProductSpecLines.Remove(line);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Specification line deleted.";
            TempData["ActiveTab"] = "specs";
            return RedirectToAction(nameof(Details), new { id = productId });
        }

        public ActionResult GetDocuments()
        {
            var products = _context.Product.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(ProductStatus)).Cast<ProductStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            if (products != null && products.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = products.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public ActionResult GetDocumentsForBarchart()
        {
            var products = _context.Product.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();
            var dates = products.Select(x => x.EditedOn.ToSite().Date).Distinct().ToList();

            if (products != null && products.Count > 0)
            {
                foreach (var date in dates)
                {
                    labels.Add(date.ToShortDateString());
                    var productCount = products.Where(x => x.EditedOn.ToSite().Date == date).Count();
                    dataSet.Data.Add(productCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);

            return Json(new { dataSet = dataSets, labels });
        }

        public async Task<IActionResult> ExportProductDetail(int id)
        {
            var product = await _context.Product
                .Where(x => x.Id == id)
                .Include(a => a.Processes)
                .Include(a => a.Changes)
                .Include(a => a.Deviations)
                .Include(a => a.Capas)
                .Include(a => a.Strengths)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();
            }

            var processList   = product.Processes?.ToList()  ?? new List<Models.Process>();
            var changeList    = product.Changes?.ToList()    ?? new List<Change>();
            var deviationList = product.Deviations?.ToList() ?? new List<Deviation>();
            var capaList      = product.Capas?.ToList()      ?? new List<Capa>();

            var ptLabel = product.ProductType.HasValue ? product.ProductType.Value switch {
                Models.ProductType.MedicalDevice  => "Medical Device",
                Models.ProductType.ConsumerHealth => "Consumer Health",
                Models.ProductType.RawMaterial    => "Raw Material",
                _ => product.ProductType.Value.ToString()
            } : null;

            var fields = new List<(string, string?)>
            {
                ("Product ID",                    product.ProductId),
                ("Name",                          product.Name),
                ("Product Type",                  ptLabel),
                ("INN / Generic Name",            product.InnName),
                ("INCI Name / Ingredient Ref.",   product.InciName),
                ("Standard / Scientific Name",    product.StandardScientificName),
                ("Brand / Trade Name",            product.BrandName),
                ("Dosage Form",                   product.DosageForm),
                ("Presentation / Form",           product.PresentationForm),
                ("Device Type / Category",        product.DeviceTypeCategory),
                ("Model / Version",               product.ModelVersion),
                ("Material Type / Category",      product.RawMaterialTypeCategory),
                ("Grade",                         product.Grade),
                ("ARTG / Regulatory Reference",   product.ArtgReference),
                ("Strength / Presentation",       string.Join(", ", product.Strengths?.Select(s => s.Value) ?? Enumerable.Empty<string>())),
                ("Status",                        product.Status.ToString()),
                ("Description",                   product.Description),
                ("Edited By",                     product.EditedBy),
                ("Edited On",                     product.EditedOn.ToSite().ToShortDateString()),
                ("Approved By",                   product.ApprovedBy),
                ("Approved On",                   product.ApprovedOn.ToSite()?.ToShortDateString()),
            };

            var tables = new List<(string, string[], IEnumerable<string[]>)>
            {
                ("Processes",
                 new[] { "Process ID", "Name", "Status" },
                 processList.Select(p => new[] { p.ProcessId, p.Name, p.Status.ToString() })),
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
            var result = PdfReportService.Generate("Product Detail Report", product.ProductId, product.Status.ToString(), fields, tables,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Product-{product.ProductId}.pdf");
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            Product? product = _context.Product
                .Include(p => p.Strengths)
                .FirstOrDefault(x => x.Id == id);

            if (product == null) return NotFound();

            var strengths = string.Join(" | ", product.Strengths?.Select(s => s.Value) ?? Enumerable.Empty<string>());
            var builder = new StringBuilder();
            builder.AppendLine("Product Id,Name,Product Type,INN/Generic Name,INCI Name,Standard/Scientific Name,Brand/Trade Name,Dosage Form,Presentation/Form,Device Type/Category,Model/Version,Material Type/Category,Grade,ARTG Reference,Strength/Presentation,Description,Version,Status,Edited By,Edited On,Approved By,Approved On");
            builder.AppendLine(string.Join(",",
                product.ProductId,
                $"\"{product.Name}\"",
                $"\"{product.ProductType?.ToString() ?? ""}\"",
                $"\"{product.InnName ?? ""}\"",
                $"\"{product.InciName ?? ""}\"",
                $"\"{product.StandardScientificName ?? ""}\"",
                $"\"{product.BrandName ?? ""}\"",
                $"\"{product.DosageForm ?? ""}\"",
                $"\"{product.PresentationForm ?? ""}\"",
                $"\"{product.DeviceTypeCategory ?? ""}\"",
                $"\"{product.ModelVersion ?? ""}\"",
                $"\"{product.RawMaterialTypeCategory ?? ""}\"",
                $"\"{product.Grade ?? ""}\"",
                $"\"{product.ArtgReference ?? ""}\"",
                $"\"{strengths}\"",
                $"\"{product.Description}\"",
                product.Version,
                product.Status,
                product.EditedBy ?? "",
                product.EditedOn.ToSite().ToShortDateString(),
                product.ApprovedBy ?? "",
                product.ApprovedOn.ToSite()?.ToShortDateString() ?? ""));

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Product.csv");
        }

        private bool ProductExists(int id)
        {
            return (_context.Product?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MasterDataAuditLog.Add(new MasterDataAuditLog
            {
                EntityType = "Product",
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
