using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Services;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.ViewModels;
using static OpenQMS.Models.Customer;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public CustomersController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: Customers/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var today = SiteTime.Today;

            var all = await _context.Customer
                .Where(c => !c.IsDeleted)
                .ToListAsync();

            var approved = all.Where(c => c.Status == CustomerStatus.Approved).ToList();

            ViewBag.TotalCustomers    = all.Count;
            ViewBag.ApprovedCount     = approved.Count;
            ViewBag.NoExpiryCount     = approved.Count(c => !c.ContractExpiryDate.HasValue);
            ViewBag.ActiveCount       = approved.Count(c => !c.ContractExpiryDate.HasValue || c.ContractExpiryDate.Value.Date >= today);
            ViewBag.ExpiringCount     = approved.Count(c => c.ContractExpiryDate.HasValue
                                                         && c.ContractExpiryDate.Value.Date >= today
                                                         && c.ContractExpiryDate.Value.Date <= today.AddDays(90));
            ViewBag.ExpiredCount      = approved.Count(c => c.ContractExpiryDate.HasValue
                                                         && c.ContractExpiryDate.Value.Date < today);

            ViewBag.TypeCounts = all
                .GroupBy(c => c.CustomerType.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.Expiring90 = approved
                .Where(c => c.ContractExpiryDate.HasValue
                         && c.ContractExpiryDate.Value.Date >= today
                         && c.ContractExpiryDate.Value.Date <= today.AddDays(90))
                .OrderBy(c => c.ContractExpiryDate)
                .Take(10)
                .ToList();

            ViewBag.Expired = approved
                .Where(c => c.ContractExpiryDate.HasValue && c.ContractExpiryDate.Value.Date < today)
                .OrderBy(c => c.ContractExpiryDate)
                .Take(10)
                .ToList();

            return View();
        }

        // GET: Customers/ContractExpiryReport
        public async Task<IActionResult> ContractExpiryReport(int days = 90)
        {
            var today  = SiteTime.Today;
            var cutoff = today.AddDays(days);

            var customers = await _context.Customer
                .Where(c => !c.IsDeleted
                         && c.Status == CustomerStatus.Approved
                         && c.ContractExpiryDate.HasValue
                         && c.ContractExpiryDate.Value.Date <= cutoff)
                .OrderBy(c => c.ContractExpiryDate)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add($"Expiring Within {days} Days");

            var headers = new[]
            {
                "Customer ID", "Legal Name", "Trading Name", "Type", "Industry",
                "Contract Reference", "Contract Expiry", "Days To Expiry",
                "Contact Name", "Contact Email", "Status"
            };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            int row = 2;
            foreach (var c in customers)
            {
                int daysLeft = (c.ContractExpiryDate!.Value.Date - today).Days;

                ws.Cell(row,  1).Value = c.CustomerId;
                ws.Cell(row,  2).Value = c.LegalName;
                ws.Cell(row,  3).Value = c.TradingName ?? "";
                ws.Cell(row,  4).Value = c.CustomerType.ToString();
                ws.Cell(row,  5).Value = c.Industry ?? "";
                ws.Cell(row,  6).Value = c.ContractReference ?? "";
                ws.Cell(row,  7).Value = c.ContractExpiryDate.Value.ToString("dd/MM/yyyy");
                ws.Cell(row,  8).Value = daysLeft;
                ws.Cell(row,  9).Value = c.ContactName ?? "";
                ws.Cell(row, 10).Value = c.ContactEmail ?? "";
                ws.Cell(row, 11).Value = c.Status.ToString();

                var fill = daysLeft < 0    ? XLColor.FromHtml("#f8d7da")
                         : daysLeft <= 30  ? XLColor.FromHtml("#ffe5d0")
                         : daysLeft <= 90  ? XLColor.FromHtml("#fff3cd")
                         : XLColor.FromHtml("#d1e7dd");
                ws.Row(row).Style.Fill.BackgroundColor = fill;

                row++;
            }

            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#343a40");
            ws.Row(1).Style.Font.FontColor = XLColor.White;
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            string fileName = $"CustomerContractExpiryReport_{days}d_{SiteTime.Today:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: Customers
        public async Task<IActionResult> Index()
        {
            var customersList = await _context.Customer.Where(c => !c.IsDeleted).ToListAsync();
            return View(customersList);
        }

        // GET: Customers/Details/5
        private async Task SyncCustomerRelatedDocs(int customerId, int[] docIds)
        {
            _context.CustomerLinkedDocument.RemoveRange(
                await _context.CustomerLinkedDocument.Where(x => x.CustomerId == customerId).ToListAsync());
            foreach (var docId in docIds.Distinct())
            {
                var doc = await _context.AppDocument.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null)
                    _context.CustomerLinkedDocument.Add(new CustomerLinkedDocument
                        { CustomerId = customerId, AppDocumentId = docId, LinkedVersion = doc.Version });
            }
        }

        private void PopulateCustomerRelatedDocsViewData(int[]? selectedIds = null)
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

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Customer == null)
                return NotFound();

            var customer = await _context.Customer
                .Include(c => c.Documents)
                .Include(c => c.Complaints)
                .Include(c => c.RelatedDocuments!).ThenInclude(rd => rd.Document)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
                return NotFound();

            ViewBag.AuditLogs = await _context.MasterDataAuditLog
                .Where(l => l.EntityType == "Customer" && l.ParentId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            return View(customer);
        }

        // POST: Customers/Details/5 — Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, string InputEmail, string InputPassword)
        {
            var customer = await _context.Customer.FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null)
                return NotFound();

            try
            {
                var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole);
                if (!isAuthorized)
                {
                    TempData["WorkflowError"] = "You do not have permission to perform this action.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var userSigning = await _userManager.FindByEmailAsync(InputEmail);
                var loggedUser = _userManager.GetUserName(User);
                if (userSigning == null || userSigning.UserName != loggedUser)
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

                if (customer.Status != CustomerStatus.UnderReview)
                {
                    TempData["WorkflowError"] = "Only customers under review can be approved.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                customer.Version = Decimal.ToInt32(customer.Version) + 1;
                customer.ApprovedBy = _userManager.GetUserName(User);
                customer.ApprovedOn = DateTime.UtcNow;
                customer.Status = CustomerStatus.Approved;
                customer.ExportFilePath = string.Empty;

                if (customer.GeneratedFrom != null && !string.IsNullOrEmpty(customer.GeneratedFrom))
                {
                    var oldCustomer = await _context.Customer.FirstOrDefaultAsync(m => m.Id.ToString() == customer.GeneratedFrom);
                    if (oldCustomer != null)
                    {
                        oldCustomer.IsLocked = false;
                        oldCustomer.Status = CustomerStatus.Obsolete;
                        _context.Customer.Update(oldCustomer);
                    }
                }

                _context.Customer.Update(customer);
                AddAuditLog(customer.Id, "Approved", "UnderReview", "Approved");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Customer approved successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(customer.Id))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Customers/SubmitForReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> SubmitForReview(int id)
        {
            var customer = await _context.Customer.FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null) return NotFound();

            if (customer.Status != CustomerStatus.Draft)
            {
                TempData["WorkflowError"] = "Only Draft customers can be submitted for review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            customer.Status = CustomerStatus.UnderReview;
            _context.Customer.Update(customer);
            AddAuditLog(customer.Id, "Submitted for review", "Draft", "UnderReview");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Customer submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Customers/SendBackToDraft/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityManager,Administrator")]
        public async Task<IActionResult> SendBackToDraft(int id)
        {
            var customer = await _context.Customer.FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null) return NotFound();

            var isAuthorized = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole);
            if (!isAuthorized)
            {
                TempData["WorkflowError"] = "You do not have permission to send back for rework.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (customer.Status != CustomerStatus.UnderReview)
            {
                TempData["WorkflowError"] = "Only customers under review can be sent back to draft.";
                return RedirectToAction(nameof(Details), new { id });
            }

            customer.Status = CustomerStatus.Draft;
            _context.Customer.Update(customer);
            AddAuditLog(customer.Id, "Sent back to draft", "UnderReview", "Draft");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Customer sent back to Draft for rework.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Customers/Create
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public IActionResult Create()
        {
            PopulateCustomerRelatedDocsViewData();
            return View();
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Create(
            [Bind("LegalName,TradingName,CustomerType,Industry,LegalId,Website,Street,City,State,PostCode,Country,ContactName,ContactEmail,ContactPhone,AccountNumber,ContractReference,ContractExpiryDate,SpecialRequirements")] Customer customer, int[]? selectedRelatedDocs)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var prevId = _context.Customer
                        .AsEnumerable()
                        .Where(x => !string.IsNullOrEmpty(x.CustomerId))
                        .Select(x => {
                            var parts = x.CustomerId.Split('-');
                            return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    customer.CustomerId = $"CST-{(prevId + 1).ToString().PadLeft(4, '0')}";
                    customer.EditedBy = _userManager.GetUserName(User);
                    customer.EditedOn = DateTime.UtcNow;
                    customer.Version = Convert.ToDecimal(0.01);
                    customer.IsLocked = false;
                    customer.Status = CustomerStatus.Draft;

                    _context.Add(customer);
                    await _context.SaveChangesAsync();
                    await SyncCustomerRelatedDocs(customer.Id, selectedRelatedDocs ?? Array.Empty<int>());
                    AddAuditLog(customer.Id, "Created", null, "Draft",
                        $"ID: {customer.CustomerId}; Legal name: {customer.LegalName}");
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id = customer.Id });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            PopulateCustomerRelatedDocsViewData(selectedRelatedDocs);
            return View(customer);
        }

        // GET: Customers/Edit/5
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Customer == null)
                return NotFound();

            var customer = await _context.Customer
                .Include(c => c.RelatedDocuments!)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null)
                return NotFound();

            if (customer.IsLocked || customer.Status == CustomerStatus.Obsolete)
                return Forbid();

            if (customer.Status == CustomerStatus.UnderReview)
            {
                TempData["Error"] = "This customer is currently under review and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var relDocIds = customer.RelatedDocuments?.Select(rd => rd.AppDocumentId).ToArray();
            PopulateCustomerRelatedDocsViewData(relDocIds);
            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QualityUser,QualityManager,Administrator")]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,CustomerId,GeneratedFrom,LegalName,TradingName,CustomerType,Industry,LegalId,Website,Street,City,State,PostCode,Country,ContactName,ContactEmail,ContactPhone,AccountNumber,ContractReference,ContractExpiryDate,SpecialRequirements,Version,EditedBy,EditedOn,ApprovedBy,ApprovedOn,IsLocked,Status")] Customer customer, int[]? selectedRelatedDocs)
        {
            if (id != customer.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Lock/status decisions and system-managed fields come from the stored record,
                    // never from the posted (hidden) form values.
                    var current = await _context.Customer.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    if (current == null) return NotFound();
                    if (current.IsLocked || current.Status == CustomerStatus.Obsolete) return Forbid();
                    if (current.Status == CustomerStatus.UnderReview)
                    {
                        TempData["Error"] = "This customer is currently under review and cannot be edited.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    customer.IsLocked      = current.IsLocked;
                    customer.Status        = current.Status;
                    customer.Version       = current.Version;
                    customer.ApprovedBy    = current.ApprovedBy;
                    customer.ApprovedOn    = current.ApprovedOn;
                    customer.GeneratedFrom = current.GeneratedFrom;
                    customer.CustomerId = current.CustomerId;
                    customer.IsDeleted     = current.IsDeleted;
                    customer.DeletedBy     = current.DeletedBy;
                    customer.DeletedOn     = current.DeletedOn;

                    // Field-level change tracking for the audit log
                    var snap = await _context.Customer.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff,     "Legal Name",           snap.LegalName,           customer.LegalName);
                        AuditDiffHelper.Track(diff,     "Trading Name",         snap.TradingName,         customer.TradingName);
                        AuditDiffHelper.TrackEnum(diff, "Customer Type",        snap.CustomerType,        customer.CustomerType);
                        AuditDiffHelper.Track(diff,     "Industry",             snap.Industry,            customer.Industry);
                        AuditDiffHelper.Track(diff,     "Legal ID",             snap.LegalId,             customer.LegalId);
                        AuditDiffHelper.Track(diff,     "Website",              snap.Website,             customer.Website);
                        AuditDiffHelper.Track(diff,     "Street",               snap.Street,              customer.Street);
                        AuditDiffHelper.Track(diff,     "City",                 snap.City,                customer.City);
                        AuditDiffHelper.Track(diff,     "State",                snap.State,               customer.State);
                        AuditDiffHelper.Track(diff,     "Post Code",            snap.PostCode,            customer.PostCode);
                        AuditDiffHelper.Track(diff,     "Country",              snap.Country,             customer.Country);
                        AuditDiffHelper.Track(diff,     "Contact Name",         snap.ContactName,         customer.ContactName);
                        AuditDiffHelper.Track(diff,     "Contact Email",        snap.ContactEmail,        customer.ContactEmail);
                        AuditDiffHelper.Track(diff,     "Contact Phone",        snap.ContactPhone,        customer.ContactPhone);
                        AuditDiffHelper.Track(diff,     "Account Number",       snap.AccountNumber,       customer.AccountNumber);
                        AuditDiffHelper.Track(diff,     "Contract Reference",   snap.ContractReference,   customer.ContractReference);
                        AuditDiffHelper.TrackDate(diff, "Contract Expiry Date", snap.ContractExpiryDate,  customer.ContractExpiryDate);
                        AuditDiffHelper.Track(diff,     "Special Requirements", snap.SpecialRequirements, customer.SpecialRequirements);
                    }
                    var editNotes = AuditDiffHelper.Build(diff);

                    customer.Version += Convert.ToDecimal(0.01);
                    customer.EditedBy = _userManager.GetUserName(User);
                    customer.EditedOn = DateTime.UtcNow;
                    customer.ExportFilePath = string.Empty;

                    if (customer.Status == CustomerStatus.Approved)
                    {
                        // Create a new Draft copy; lock the current Approved record
                        var newCustomer = new Customer
                        {
                            CustomerId = customer.CustomerId,
                            GeneratedFrom = id.ToString(),
                            LegalName = customer.LegalName,
                            TradingName = customer.TradingName,
                            CustomerType = customer.CustomerType,
                            Industry = customer.Industry,
                            LegalId = customer.LegalId,
                            Website = customer.Website,
                            Street = customer.Street,
                            City = customer.City,
                            State = customer.State,
                            PostCode = customer.PostCode,
                            Country = customer.Country,
                            ContactName = customer.ContactName,
                            ContactEmail = customer.ContactEmail,
                            ContactPhone = customer.ContactPhone,
                            AccountNumber = customer.AccountNumber,
                            ContractReference = customer.ContractReference,
                            ContractExpiryDate = customer.ContractExpiryDate,
                            SpecialRequirements = customer.SpecialRequirements,
                            Version = customer.Version,
                            EditedBy = customer.EditedBy,
                            EditedOn = customer.EditedOn,
                            Status = CustomerStatus.Draft,
                            ApprovedBy = null,
                            ApprovedOn = null,
                            IsLocked = false
                        };
                        _context.Add(newCustomer);
                        await _context.SaveChangesAsync();
                        await SyncCustomerRelatedDocs(newCustomer.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        AddAuditLog(newCustomer.Id, "Edited (new revision)", "Approved", "Draft", editNotes);
                        await _context.SaveChangesAsync();

                        var oldCustomer = await _context.Customer.FirstOrDefaultAsync(m => m.Id == id);
                        if (oldCustomer != null)
                        {
                            oldCustomer.IsLocked = true;
                            _context.Update(oldCustomer);
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        customer.Status = CustomerStatus.Draft;
                        _context.Update(customer);
                        await SyncCustomerRelatedDocs(customer.Id, selectedRelatedDocs ?? Array.Empty<int>());
                        if (diff.Count > 0) AddAuditLog(customer.Id, "Edited", "Draft", "Draft", editNotes);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Details), new { id = customer.Id });
            }
            PopulateCustomerRelatedDocsViewData(selectedRelatedDocs);
            return View(customer);
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Customer == null)
                return NotFound();

            var customer = await _context.Customer
                .Include(c => c.Documents)
                .Include(c => c.Complaints)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
                return NotFound();

            if (customer.Status != CustomerStatus.Draft)
            {
                TempData["Error"] = "Only Draft customers can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["HasLinkedRecords"] = customer.Documents?.Any() == true || customer.Complaints?.Any() == true;

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Customer == null)
                return Problem("Entity set 'ApplicationDbContext.Customer' is null.");

            var customer = await _context.Customer
                .Include(c => c.Documents)
                .Include(c => c.Complaints)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
                return NotFound();

            if (customer.Status != CustomerStatus.Draft)
            {
                TempData["Error"] = "Only Draft customers can be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (customer.Documents?.Any() == true || customer.Complaints?.Any() == true)
            {
                TempData["ErrorMessage"] = "This customer has linked Complaints or uploaded Documents. Remove them before deleting.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            AddAuditLog(customer.Id, "Deleted", customer.Status.ToString(), null, customer.LegalName);
            customer.IsDeleted = true;
            customer.DeletedBy = _userManager.GetUserName(User);
            customer.DeletedOn = DateTime.UtcNow;
            _context.Customer.Update(customer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // DOCUMENTS
        // ============================================================

        public async Task<IActionResult> Documents(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customer
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
                return NotFound();

            if (customer.Documents != null)
            {
                bool changed = false;
                foreach (var doc in customer.Documents)
                {
                    CustomerDocumentStatus newStatus;
                    if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow)
                        newStatus = CustomerDocumentStatus.Expired;
                    else if (doc.ExpiryDate.HasValue && doc.ExpiryDate < DateTime.UtcNow.AddDays(doc.ReminderDays))
                        newStatus = CustomerDocumentStatus.Expiring;
                    else
                        newStatus = CustomerDocumentStatus.Valid;

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
            ViewBag.DeletedFiles = await _context.CustomerDocument.DeletedOnly().Where(f => f.CustomerId == id).ToListAsync();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> UploadDocument(int customerId, IFormFile file, string? description, DateTime? issueDate, DateTime? expiryDate)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Documents), new { id = customerId });
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed. Accepted: PDF, DOC, DOCX, ZIP, JPG, JPEG, PNG.";
                return RedirectToAction(nameof(Documents), new { id = customerId });
            }

            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds the 25 MB maximum size limit.";
                return RedirectToAction(nameof(Documents), new { id = customerId });
            }

            var customer = await _context.Customer.FindAsync(customerId);
            if (customer == null)
                return NotFound();

            if (customer.Status != CustomerStatus.Draft && customer.Status != CustomerStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be uploaded to Draft or Under Review customer records.";
                return RedirectToAction(nameof(Documents), new { id = customerId });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "customers", customerId.ToString());
            Directory.CreateDirectory(uploadDir);

            var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var doc = new CustomerDocument
            {
                CustomerId = customerId,
                FileName = file.FileName,
                FilePath = $"/uploads/customers/{customerId}/{uniqueName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = description,
                UploadedBy = _userManager.GetUserName(User),
                UploadedDate = DateTime.UtcNow,
                DocumentStatus = CustomerDocumentStatus.Valid
            };

            _context.CustomerDocument.Add(doc);
            AddAuditLog(customerId, "Document uploaded", null, null, file.FileName);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(Documents), new { id = customerId });
        }

        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.CustomerDocument.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (doc == null)
                return NotFound();

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(fullPath, doc.FileHash))
            {
                AddAuditLog(doc.CustomerId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                return RedirectToAction("Documents", new { id = doc.CustomerId });
            }

            AddAuditLog(doc.CustomerId, (deleted ? "Deleted document downloaded" : "Document downloaded"), null, null, doc.FileName);
            await _context.SaveChangesAsync();

            var parent  = await _context.Customer.FindAsync(doc.CustomerId);
            var version = parent?.Version.ToString("0.##");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Customer",
                doc.CustomerId.ToString(), version,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, doc.ContentType ?? "application/octet-stream", doc.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> DeleteDocument(int id, int customerId, string? deletionReason)
        {
            var doc = await _context.CustomerDocument.FindAsync(id);
            if (doc == null)
                return NotFound();

            var customer = await _context.Customer.FindAsync(doc.CustomerId);
            if (customer == null)
                return NotFound();

            if (customer.Status != CustomerStatus.Draft && customer.Status != CustomerStatus.UnderReview)
            {
                TempData["Error"] = "Documents can only be deleted from Draft or Under Review customer records.";
                return RedirectToAction(nameof(Documents), new { id = doc.CustomerId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                return RedirectToAction(nameof(Documents), new { id = doc.CustomerId });
            }

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();
            AddAuditLog(doc.CustomerId, "Document deleted", null, null, $"{doc.FileName} — Reason: {doc.DeletionReason}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Documents), new { id = doc.CustomerId });
        }

        // ============================================================
        // EXPORT
        // ============================================================

        public async Task<IActionResult> ExportCustomerDetail(int id)
        {
            var customer = await _context.Customer
                .Where(x => x.Id == id)
                .Include(c => c.Complaints)
                .FirstOrDefaultAsync();

            if (customer == null)
            {
                return NotFound();
            }

            var complaints = customer.Complaints?.ToList() ?? new List<Complaint>();

            var fields = new List<(string, string?)>
            {
                ("Customer ID",        customer.CustomerId),
                ("Status",             customer.Status.ToString()),
                ("Version",            customer.Version.ToString()),
                ("Legal Name",         customer.LegalName),
                ("Trading Name",       customer.TradingName),
                ("Customer Type",      customer.CustomerType.ToString()),
                ("Industry",           customer.Industry),
                ("Legal ID",           customer.LegalId),
                ("Website",            customer.Website),
                ("Address",            string.Join(", ", new[] { customer.Street, customer.City, customer.State, customer.PostCode, customer.Country }.Where(s => !string.IsNullOrEmpty(s)))),
                ("Contact Name",       customer.ContactName),
                ("Contact Email",      customer.ContactEmail),
                ("Contact Phone",      customer.ContactPhone),
                ("Account Number",     customer.AccountNumber),
                ("Contract Reference", customer.ContractReference),
                ("Contract Expiry",    customer.ContractExpiryDate?.ToString("dd/MM/yyyy")),
                ("Special Requirements", customer.SpecialRequirements),
                ("Edited By",          customer.EditedBy),
                ("Edited On",          customer.EditedOn.ToSite().ToString("dd/MM/yyyy")),
                ("Approved By",        customer.ApprovedBy),
                ("Approved On",        customer.ApprovedOn.ToSite()?.ToString("dd/MM/yyyy")),
            };

            var tables = new List<(string, string[], IEnumerable<string[]>)>
            {
                ("Complaints",
                 new[] { "Complaint ID", "Title", "Severity", "Status" },
                 complaints.Select(c => new[] { c.ComplaintId, c.Title, c.Severity.ToString(), c.Status.ToString() })),
            };

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate("Customer Detail Report", customer.CustomerId, customer.Status.ToString(), fields, tables,
                orgName: _s?.OrganisationName, orgAddress: _s?.OrganisationAddress, logoPhysicalPath: _logo);
            return File(result, "application/pdf", $"Customer-{customer.CustomerId}.pdf");
        }

        public ActionResult ExportDetailInCSV(int id)
        {
            var customer = _context.Customer.FirstOrDefault(x => x.Id == id);
            if (customer == null)
                return NotFound();

            var builder = new StringBuilder();
            builder.AppendLine("Customer Id,Legal Name,Trading Name,Type,Industry,Contact Name,Contact Email,Version,Status,Edited By,Edited On,Approved By,Approved On");
            builder.AppendLine($"{customer.CustomerId},{customer.LegalName},{customer.TradingName ?? ""},{customer.CustomerType},{customer.Industry ?? ""},{customer.ContactName ?? ""},{customer.ContactEmail ?? ""},{customer.Version},{customer.Status},{customer.EditedBy ?? ""},{customer.EditedOn.ToSite().ToShortDateString()},{customer.ApprovedBy ?? ""},{(customer.ApprovedOn.HasValue ? customer.ApprovedOn.Value.ToSite().ToShortDateString() : "")}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Customer.csv");
        }

        public ActionResult GetDocuments()
        {
            var customers = _context.Customer.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var statusList = Enum.GetValues(typeof(CustomerStatus)).Cast<CustomerStatus>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();

            if (customers != null && customers.Count > 0)
            {
                foreach (var label in statusList)
                {
                    labels.Add(label.ToString());
                    var statusCount = customers.Where(x => x.Status.Equals(label)).Count();
                    dataSet.Data.Add(statusCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);
            return Json(new { dataSet = dataSets, labels });
        }

        public ActionResult GetDocumentsForBarchart()
        {
            var customers = _context.Customer.ToList();
            var dataSets = new List<PoliciesChartViewModel>();
            var labels = new List<string>();
            var random = new Random();
            var dataSet = new PoliciesChartViewModel();
            var dates = customers.Select(x => x.EditedOn.ToSite().Date).Distinct().ToList();

            if (customers != null && customers.Count > 0)
            {
                foreach (var date in dates)
                {
                    labels.Add(date.ToShortDateString());
                    var customerCount = customers.Where(x => x.EditedOn.ToSite().Date == date).Count();
                    dataSet.Data.Add(customerCount);
                    dataSet.BackgroundColor.Add(String.Format("#{0:X6}", random.Next(0x1000000)));
                }
            }

            dataSets.Add(dataSet);
            return Json(new { dataSet = dataSets, labels });
        }

        private bool CustomerExists(int id)
        {
            return (_context.Customer?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MasterDataAuditLog.Add(new MasterDataAuditLog
            {
                EntityType = "Customer",
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
