using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OpenQMS.Data;
using OpenQMS;
using OpenQMS.Models;
using OpenQMS.Models.LC;
using OpenQMS.Models.RI;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class LicencesCertificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly IWebHostEnvironment _env;
        private readonly LicCertWorkflowService _workflow;
        private readonly LicCertNotificationService _notification;

        public LicencesCertificationsController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            IPasswordHasher<AppUser> passwordHasher,
            IWebHostEnvironment env,
            LicCertWorkflowService workflow,
            LicCertNotificationService notification)
        {
            _context        = context;
            _userManager    = userManager;
            _passwordHasher = passwordHasher;
            _env            = env;
            _workflow       = workflow;
            _notification   = notification;
        }

        // ── REGISTER (INDEX) ──────────────────────────────────────────────────

        // GET /LicencesCertifications
        public async Task<IActionResult> Index(
            string? category, string? holderType, string? status,
            int? licenceTypeId, string? expiryWindow)
        {
            var query = _context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted)
                .Include(r => r.LicenceType)
                .Include(r => r.Supplier)
                .Include(r => r.Conditions.Where(c => !c.IsDeleted))
                .AsQueryable();

            if (!string.IsNullOrEmpty(category) && Enum.TryParse<LCCategory>(category, out var cat))
                query = query.Where(r => r.Category == cat);

            if (!string.IsNullOrEmpty(holderType) && Enum.TryParse<LCHolderType>(holderType, out var ht))
                query = query.Where(r => r.HolderType == ht);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<LCStatus>(status, out var st))
                query = query.Where(r => r.Status == st);

            if (licenceTypeId.HasValue)
                query = query.Where(r => r.LicenceTypeId == licenceTypeId.Value);

            if (!string.IsNullOrEmpty(expiryWindow))
            {
                var today = SiteTime.Today;
                int days = expiryWindow switch { "30" => 30, "90" => 90, "180" => 180, _ => 365 };
                query = query.Where(r => r.ExpiryDate.HasValue
                    && r.ExpiryDate.Value.Date >= today
                    && r.ExpiryDate.Value.Date <= today.AddDays(days));
            }

            var records = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            ViewBag.SelectedCategory   = category;
            ViewBag.SelectedHolderType = holderType;
            ViewBag.SelectedStatus     = status;
            ViewBag.SelectedTypeId     = licenceTypeId;
            ViewBag.SelectedExpiry     = expiryWindow;

            ViewBag.LicenceTypes = new SelectList(
                await _context.LCLicenceTypes.Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ToListAsync(),
                "Id", "DisplayName", licenceTypeId);

            return View(records);
        }

        // ── CREATE ────────────────────────────────────────────────────────────

        // GET /LicencesCertifications/Create
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Create()
        {
            await PopulateViewData(null);
            var settings = await _context.SystemSettings.FindAsync(1);
            var orgName  = settings?.OrganisationName ?? "";
            ViewBag.OrganisationName = orgName;
            return View(new LicenceCertificationRecord { HolderName = orgName });
        }

        // POST /LicencesCertifications/Create
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Create(LicenceCertificationRecord model)
        {
            ModelState.Remove("LCNumber");
            ModelState.Remove("LicenceType");
            ModelState.Remove("Supplier");

            if (!ModelState.IsValid)
            {
                await PopulateViewData(null);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            model.LCNumber  = await GenerateLCNumber();
            model.CreatedBy = user?.UserName;
            model.CreatedAt = DateTime.UtcNow;
            model.IsLocked  = false;

            // Auto-populate renewal lead time from type default if not overridden
            var licType = await _context.LCLicenceTypes.FindAsync(model.LicenceTypeId);
            if (licType != null && model.RenewalLeadTimeDays == 0)
                model.RenewalLeadTimeDays = licType.DefaultRenewalLeadTimeDays;

            _context.LicenceCertificationRecords.Add(model);
            await _context.SaveChangesAsync();

            // Audit log
            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = model.Id,
                Action    = "Record created",
                Details   = $"ID: {model.LCNumber}; Status: {model.Status}; Holder: {model.HolderName}",
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Licence/Certification record {model.LCNumber} created.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ──────────────────────────────────────────────────────────────

        // GET /LicencesCertifications/Edit/5
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int id)
        {
            var record = await _context.LicenceCertificationRecords
                .Include(r => r.LicenceType)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (record == null) return NotFound();
            if (record.IsLocked)
            {
                TempData["Error"] = LicCertWorkflowService.IsClosed(record.Status)
                    ? "This record is closed and cannot be edited."
                    : "This record is locked and cannot be edited. Create a Variation to make changes.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await PopulateViewData(record);
            var settings = await _context.SystemSettings.FindAsync(1);
            ViewBag.OrganisationName = settings?.OrganisationName ?? "";
            return View(record);
        }

        // POST /LicencesCertifications/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int id, LicenceCertificationRecord model)
        {
            if (id != model.Id) return NotFound();

            ModelState.Remove("LicenceType");
            ModelState.Remove("Supplier");

            // Load untracked snapshot for diff — must be before the tracked FindAsync
            var snap = await _context.LicenceCertificationRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (snap == null) return NotFound();

            var existing = await _context.LicenceCertificationRecords.FindAsync(id);
            if (existing == null || existing.IsDeleted) return NotFound();

            if (existing.IsLocked)
            {
                TempData["Error"] = LicCertWorkflowService.IsClosed(existing.Status)
                    ? "This record is closed and cannot be edited."
                    : "This record is locked and cannot be edited. Create a Variation to make changes.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                await PopulateViewData(model);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            // Preserve immutable fields
            model.LCNumber  = existing.LCNumber;
            model.CreatedBy = existing.CreatedBy;
            model.CreatedAt = existing.CreatedAt;
            model.Status    = existing.Status;
            model.IsLocked  = existing.IsLocked;
            model.IsDeleted = existing.IsDeleted;
            model.ModifiedBy = user?.UserName;
            model.ModifiedAt = DateTime.UtcNow;

            // Justification / Reason is owned by the workflow (set when a record is
            // Suspended, Withdrawn, or marked Not Applicable). The Edit form shows it
            // read-only, so preserve the stored value rather than letting a save blank it.
            model.JustificationReason = existing.JustificationReason;

            // Build field-level diff
            var diff = new List<string>();
            AuditDiffHelper.TrackEnum(diff, "Holder Type",          snap.HolderType,                  model.HolderType);
            AuditDiffHelper.Track    (diff, "Holder Name",          snap.HolderName,                  model.HolderName);
            AuditDiffHelper.Track    (diff, "Site Country",         snap.LinkedSiteCountry,            model.LinkedSiteCountry);
            AuditDiffHelper.Track    (diff, "Site Address",         snap.LinkedSiteAddress,            model.LinkedSiteAddress);
            AuditDiffHelper.TrackEnum(diff, "Category",             snap.Category,                    model.Category);
            AuditDiffHelper.Track<int>(diff,"Licence Type",         snap.LicenceTypeId,               model.LicenceTypeId);
            AuditDiffHelper.Track    (diff, "Issuing Body",         snap.IssuingBody,                 model.IssuingBody);
            AuditDiffHelper.Track    (diff, "Issuing Body URL",     snap.IssuingBodyUrl,              model.IssuingBodyUrl);
            AuditDiffHelper.Track    (diff, "Authority Portal",     snap.IssuingBodyPortal,           model.IssuingBodyPortal);
            AuditDiffHelper.Track    (diff, "Licence Number",       snap.LicenceNumber,               model.LicenceNumber);
            AuditDiffHelper.TrackDate(diff, "Application Date",     snap.ApplicationDate,             model.ApplicationDate);
            AuditDiffHelper.TrackDate(diff, "Issue Date",           snap.IssueDate,                   model.IssueDate);
            AuditDiffHelper.TrackDate(diff, "Expiry Date",          snap.ExpiryDate,                  model.ExpiryDate);
            AuditDiffHelper.TrackDate(diff, "Next Renewal Due",     snap.NextRenewalDue,              model.NextRenewalDue);
            AuditDiffHelper.Track<int>(diff,"Renewal Lead Days",    snap.RenewalLeadTimeDays,         model.RenewalLeadTimeDays);
            AuditDiffHelper.Track    (diff, "Scope",                snap.Scope,                       model.Scope);
            AuditDiffHelper.TrackEnum(diff, "GMP Pathway",          snap.GmcPathway,                  model.GmcPathway);
            AuditDiffHelper.Track    (diff, "Inspection Authority", snap.InspectionAuthority,         model.InspectionAuthority);
            AuditDiffHelper.TrackDate(diff, "Last Inspection Date", snap.LastInspectionDate,          model.LastInspectionDate);
            AuditDiffHelper.TrackDate(diff, "RFI Date",             snap.RFIDate,                     model.RFIDate);
            AuditDiffHelper.TrackDate(diff, "RFI Response Due",     snap.RFIResponseDueDate,          model.RFIResponseDueDate);
            AuditDiffHelper.TrackDate(diff, "RFI Response Submitted", snap.RFIResponseSubmittedDate,  model.RFIResponseSubmittedDate);
            AuditDiffHelper.Track    (diff, "Manufacturing Scope",  snap.ManufacturingStepsScope,     model.ManufacturingStepsScope);
            AuditDiffHelper.Track<bool>(diff,"Letter of Access",    snap.LetterOfAccessAvailable,     model.LetterOfAccessAvailable);
            AuditDiffHelper.Track    (diff, "Certification Body",   snap.CertificationBody,           model.CertificationBody);
            AuditDiffHelper.Track    (diff, "Standard Version",     snap.StandardVersion,             model.StandardVersion);
            AuditDiffHelper.TrackDate(diff, "Stage 1 Audit",        snap.Stage1AuditDate,             model.Stage1AuditDate);
            AuditDiffHelper.TrackDate(diff, "Stage 2 Audit",        snap.Stage2AuditDate,             model.Stage2AuditDate);
            AuditDiffHelper.TrackDate(diff, "Next Surveillance Audit", snap.NextSurveillanceAuditDate, model.NextSurveillanceAuditDate);
            AuditDiffHelper.TrackDate(diff, "Recertification Due",  snap.RecertificationDueDate,      model.RecertificationDueDate);
            AuditDiffHelper.Track    (diff, "Certificate Scope",    snap.CertificateScope,            model.CertificateScope);
            AuditDiffHelper.Track    (diff, "Responsible Person",   snap.InternalResponsiblePersonId, model.InternalResponsiblePersonId);
            AuditDiffHelper.Track    (diff, "External Consultant",  snap.ExternalConsultant,          model.ExternalConsultant);
            AuditDiffHelper.Track    (diff, "Authority Email",      snap.AuthorityContactEmail,       model.AuthorityContactEmail);
            AuditDiffHelper.Track    (diff, "Authority Portal User",snap.AuthorityPortalUsername,     model.AuthorityPortalUsername);
            AuditDiffHelper.Track    (diff, "Justification",        snap.JustificationReason,         model.JustificationReason);
            AuditDiffHelper.Track    (diff, "Regulatory Action Notes", snap.RegulatoryActionNotes,    model.RegulatoryActionNotes);

            _context.Entry(existing).CurrentValues.SetValues(model);
            await _context.SaveChangesAsync();

            if (diff.Count > 0)
            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = id,
                Action    = "Record details updated",
                Details   = AuditDiffHelper.Build(diff),
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Record updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DETAILS ───────────────────────────────────────────────────────────

        // GET /LicencesCertifications/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var record = await _context.LicenceCertificationRecords
                .Include(r => r.LicenceType)
                .Include(r => r.Supplier)
                .Include(r => r.Conditions.Where(c => !c.IsDeleted))
                .Include(r => r.Documents.Where(d => !d.IsDeleted))
                .Include(r => r.Dependencies)
                    .ThenInclude(d => d.RegulatoryApplication)
                .Include(r => r.AuditLogs.Where(a => !a.IsDeleted))
                .Include(r => r.ApprovalSnapshots.OrderBy(s => s.RenewalCycle))
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (record == null) return NotFound();

            // Responsible person display name
            if (!string.IsNullOrEmpty(record.InternalResponsiblePersonId))
            {
                var resp = await _userManager.FindByIdAsync(record.InternalResponsiblePersonId);
                ViewBag.ResponsiblePersonName = resp != null
                    ? $"{resp.FirstName} {resp.LastName}".Trim()
                    : record.InternalResponsiblePersonId;
            }

            // Expiry alert bands
            if (record.ExpiryDate.HasValue)
            {
                int days = (record.ExpiryDate.Value.Date - SiteTime.Today).Days;
                ViewBag.ExpiryDays = days;
                ViewBag.ExpiryBand = days < 0 ? "danger" : days <= 30 ? "danger" : days <= 90 ? "warning" : "success";
            }

            // For Add Dependency modal: list RI applications not already linked
            var linkedIds = record.Dependencies.Select(d => d.RegulatoryApplicationId).ToHashSet();
            var riApps = await _context.RegulatoryApplications
                .Where(a => !a.IsDeleted && !linkedIds.Contains(a.Id))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new { a.Id, Display = $"{a.RINumber} — {a.ProductName}" })
                .ToListAsync();
            ViewBag.RIApplicationsList = new SelectList(riApps, "Id", "Display");

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.LCDocuments.DeletedOnly().Where(f => f.LicenceCertificationRecordId == id).ToListAsync();
            return View(record);
        }

        // ── DELETE ────────────────────────────────────────────────────────────

        // POST /LicencesCertifications/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int id)
        {
            var record = await _context.LicenceCertificationRecords.FindAsync(id);
            if (record == null || record.IsDeleted) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            record.IsDeleted = true;
            record.ModifiedBy = user?.UserName;
            record.ModifiedAt = DateTime.UtcNow;

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = id,
                Action    = "Record deleted (soft delete)",
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow,
                IsDeleted = true
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Record {record.LCNumber} deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── WORKFLOW: Non-password-gated transitions ──────────────────────────

        // POST /LicencesCertifications/Transition
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Transition(int id, LCStatus newStatus, string? reason = null)
        {
            var record = await _context.LicenceCertificationRecords
                .Include(r => r.Conditions)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (record == null) return NotFound();

            var result = _workflow.ValidateTransition(record, newStatus, reason);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            var performedByName = _userManager.GetUserName(User) ?? User.Identity?.Name ?? "System";
            var performedById   = _userManager.GetUserId(User)   ?? performedByName;
            await _workflow.ApplyTransition(record, newStatus, performedById, performedByName, reason);
            await FireTransitionNotification(record, newStatus, performedByName);

            TempData["Success"] = $"Status updated to: {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── WORKFLOW: Initiate Renewal Now (early / unscheduled renewal) ──────

        // POST /LicencesCertifications/InitiateRenewal
        // Lets a user start the renewal cycle before the lead-time monitor would
        // auto-transition a dated record. Captures a justification for the audit trail.
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> InitiateRenewal(int id, string reason)
        {
            var record = await _context.LicenceCertificationRecords
                .Include(r => r.Conditions)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (record == null) return NotFound();

            if (record.Status != LCStatus.IssuedActive)
            {
                TempData["Error"] = "A renewal can only be initiated from an Issued / Active record.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A reason is required to initiate an early renewal.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var note   = $"Early renewal initiated: {reason.Trim()}";
            var result = _workflow.ValidateTransition(record, LCStatus.RenewalDue, note);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            var performedByName = _userManager.GetUserName(User) ?? User.Identity?.Name ?? "System";
            var performedById   = _userManager.GetUserId(User)   ?? performedByName;
            await _workflow.ApplyTransition(record, LCStatus.RenewalDue, performedById, performedByName, note);
            await FireTransitionNotification(record, LCStatus.RenewalDue, performedByName);

            TempData["Success"] = "Renewal initiated — record moved to Renewal Due.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── WORKFLOW: Issue (password-gated) ──────────────────────────────────

        // POST /LicencesCertifications/IssueRecord
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> IssueRecord(int id, string email, string pwd)
            => await TransitionWithCredentials(id, email, pwd, LCStatus.IssuedActive, null);

        // ── WORKFLOW: Suspend (password-gated, reason required) ───────────────

        // POST /LicencesCertifications/SuspendRecord
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> SuspendRecord(int id, string email, string pwd, string reason)
            => await TransitionWithCredentials(id, email, pwd, LCStatus.Suspended, reason);

        // ── WORKFLOW: Withdraw (password-gated, reason required) ──────────────

        // POST /LicencesCertifications/WithdrawRecord
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> WithdrawRecord(int id, string email, string pwd, string reason)
            => await TransitionWithCredentials(id, email, pwd, LCStatus.Withdrawn, reason);

        // ── PRIVATE: Credential-verified transition ───────────────────────────

        private async Task<IActionResult> TransitionWithCredentials(
            int id, string email, string pwd,
            LCStatus newStatus, string? reason)
        {
            // E-signature must be made with the signed-in user's own credentials
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user = null;
            if (user == null ||
                _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, pwd) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Invalid credentials. Action not authorised.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var record = await _context.LicenceCertificationRecords
                .Include(r => r.Conditions)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (record == null) return NotFound();

            var result = _workflow.ValidateTransition(record, newStatus, reason);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.ApplyTransition(record, newStatus, user.Id.ToString(), user.UserName ?? email, reason);
            await FireTransitionNotification(record, newStatus, user.UserName ?? email);

            TempData["Success"] = $"Status updated to: {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── CONDITIONS ────────────────────────────────────────────────────────

        // POST /LicencesCertifications/AddCondition
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> AddCondition(
            int recordId, string conditionDescription, DateTime? dueDate)
        {
            var user = await _userManager.GetUserAsync(User);
            var cond = new LCCondition
            {
                LicenceCertificationRecordId = recordId,
                ConditionDescription = conditionDescription,
                DueDate    = dueDate,
                Status     = LCConditionStatus.Active,
                CreatedBy  = user?.UserName,
                CreatedAt  = DateTime.UtcNow
            };
            _context.LCConditions.Add(cond);

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = "Condition added",
                Details   = conditionDescription,
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Condition added.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // POST /LicencesCertifications/UpdateCondition
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UpdateCondition(
            int conditionId, int recordId, string conditionDescription, DateTime? dueDate)
        {
            var cond = await _context.LCConditions.FindAsync(conditionId);
            if (cond == null || cond.IsDeleted) return NotFound();

            cond.ConditionDescription = conditionDescription;
            cond.DueDate = dueDate;

            var user = await _userManager.GetUserAsync(User);
            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = "Condition updated",
                Details   = conditionDescription,
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Condition updated.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // POST /LicencesCertifications/CompleteCondition
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CompleteCondition(
            int conditionId, int recordId, string completionNotes)
        {
            var cond = await _context.LCConditions.FindAsync(conditionId);
            if (cond == null || cond.IsDeleted) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            cond.Status          = LCConditionStatus.Completed;
            cond.CompletedAt     = DateTime.UtcNow;
            cond.CompletedBy     = user?.UserName;
            cond.CompletionNotes = completionNotes;

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = "Condition completed",
                Details   = completionNotes,
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Condition marked as completed.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // POST /LicencesCertifications/DeleteCondition
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteCondition(int conditionId, int recordId)
        {
            var cond = await _context.LCConditions.FindAsync(conditionId);
            if (cond == null) return NotFound();

            cond.IsDeleted = true;

            var user = await _userManager.GetUserAsync(User);
            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = "Condition deleted",
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Condition deleted.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // POST /LicencesCertifications/WaiveCondition
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> WaiveCondition(
            int conditionId, int recordId, string waiverReason)
        {
            var cond = await _context.LCConditions.FindAsync(conditionId);
            if (cond == null || cond.IsDeleted) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            cond.Status          = LCConditionStatus.Waived;
            cond.CompletedAt     = DateTime.UtcNow;
            cond.CompletedBy     = user?.UserName;
            cond.CompletionNotes = waiverReason;

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = "Condition waived",
                Details   = waiverReason,
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Condition marked as waived.";
            TempData["ActiveTab"] = "dtab6";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // POST /LicencesCertifications/CreateCAPAFromCondition
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CreateCAPAFromCondition(int conditionId, int recordId)
        {
            var cond = await _context.LCConditions.FindAsync(conditionId);
            if (cond == null || cond.IsDeleted) return NotFound();

            var record = await _context.LicenceCertificationRecords.FindAsync(recordId);
            if (record == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            // Generate next CAPA ID — match CapasController: 4-digit, numeric max (not lexicographic)
            var allCapaIds = await _context.Capa!
                .Where(c => c.CapaId != null)
                .Select(c => c.CapaId!)
                .ToListAsync();
            int lastId = allCapaIds
                .Select(cid => { var p = cid.Split('-'); return p.Length == 2 && int.TryParse(p[1], out int n) ? n : 0; })
                .DefaultIfEmpty(0)
                .Max();

            var capa = new Capa
            {
                CapaId             = $"CPA-{(lastId + 1).ToString().PadLeft(4, '0')}",
                Title              = $"LicCert Condition — {record.LCNumber}: {cond.ConditionDescription[..Math.Min(100, cond.ConditionDescription.Length)]}",
                CorrectiveAction   = cond.ConditionDescription,
                PreventiveAction   = string.Empty,
                DeterminedBy       = user?.UserName ?? "System",
                DeterminedOn       = DateTime.UtcNow,
                Status             = Capa.CapaStatus.Determination
            };

            _context.Capa!.Add(capa);
            await _context.SaveChangesAsync();

            // Back-link the condition to the CAPA
            cond.LinkedCAPAId = capa.Id;

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = $"CAPA {capa.CapaId} raised from condition",
                Details   = cond.ConditionDescription,
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"CAPA {capa.CapaId} created from condition.";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── DOCUMENTS ─────────────────────────────────────────────────────────

        private static readonly string[] AllowedExtensions = UploadRules.AllowedExtensions;
        private const long MaxFileSizeBytes = UploadRules.MaxBytes; // 25 MB

        // POST /LicencesCertifications/UploadDocument
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UploadDocument(
            int recordId, IFormFile file, LCDocumentType documentType, string? description)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed. Accepted: {string.Join(", ", AllowedExtensions)}";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            if (file.Length > MaxFileSizeBytes)
            {
                TempData["Error"] = "File exceeds the 25 MB maximum size limit.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            var record = await _context.LicenceCertificationRecords.FindAsync(recordId);
            if (record == null) return NotFound();

            if (record.IsLocked)
            {
                TempData["Error"] = "This record is locked and cannot be modified.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "liccert", recordId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var timestamp      = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var safeName       = SanitiseFileName(Path.GetFileNameWithoutExtension(file.FileName));
            var uniqueFileName = $"{timestamp}_{safeName}{ext}";
            var filePath       = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var user = await _userManager.GetUserAsync(User);
            var doc = new LCDocument
            {
                LicenceCertificationRecordId = recordId,
                FileName      = file.FileName,
                StoragePath   = $"/uploads/liccert/{recordId}/{uniqueFileName}",
                DocumentType  = documentType,
                Description   = description,
                FileSizeBytes = file.Length,
                FileHash      = fileHash,
                UploadedById  = user?.UserName ?? user?.Email ?? user?.Id.ToString(),
                UploadedAt    = DateTime.UtcNow
            };
            _context.LCDocuments.Add(doc);

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = $"Document uploaded: {file.FileName} ({documentType})",
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Document '{file.FileName}' uploaded.";
            TempData["ActiveTab"] = "dtab7";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // GET /LicencesCertifications/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id, bool deleted = false)
        {
            var doc = await _context.LCDocuments.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == id);
            if (doc == null || doc.IsDeleted) return NotFound();

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.StoragePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(fullPath, doc.FileHash))
            {
                var integrityUser = await _userManager.GetUserAsync(User);
                _context.LCAuditLogs.Add(new LCAuditLog { LicenceCertificationRecordId = doc.LicenceCertificationRecordId, Action = $"Integrity check failed: {doc.FileName}", Details = OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName), UserId = integrityUser?.Id.ToString(), UserName = integrityUser?.UserName, Timestamp = DateTime.UtcNow });
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = doc.LicenceCertificationRecordId });
            }

            var user = await _userManager.GetUserAsync(User);
            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = doc.LicenceCertificationRecordId,
                Action    = $"Document downloaded: {doc.FileName}",
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.RegulatoryAttachment, null, "Licence/Certification",
                doc.LicenceCertificationRecordId.ToString(), null,
                user?.UserName ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, "application/octet-stream", doc.FileName);
        }

        // POST /LicencesCertifications/DeleteDocument
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteDocument(int docId, int recordId, string? deletionReason)
        {
            var doc = await _context.LCDocuments.FindAsync(docId);
            if (doc == null) return NotFound();

            // Resolve the parent from the document itself — never trust the posted id.
            recordId = doc.LicenceCertificationRecordId;

            var record = await _context.LicenceCertificationRecords.FindAsync(recordId);
            if (record != null && record.IsLocked)
            {
                TempData["Error"] = "This record is locked and cannot be modified — documents cannot be removed.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                TempData["ActiveTab"] = "dtab7";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            var user = await _userManager.GetUserAsync(User);

            // Soft delete: the row and file are retained for traceability (the file was
            // previously removed from disk while the row was only flagged).
            doc.IsDeleted      = true;
            doc.DeletedBy      = user?.UserName ?? User.Identity?.Name ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();

            _context.LCAuditLogs.Add(new LCAuditLog
            {
                LicenceCertificationRecordId = recordId,
                Action    = $"Document deleted: {doc.FileName}",
                Details   = $"Reason: {doc.DeletionReason}",
                UserId    = user?.Id.ToString(),
                UserName  = user?.UserName,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Document '{doc.FileName}' deleted.";
            TempData["ActiveTab"] = "dtab7";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── DEPENDENCIES ──────────────────────────────────────────────────────

        // POST /LicencesCertifications/AddDependency
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> AddDependency(
            int recordId, int regulatoryApplicationId, string? notes)
        {
            var lcRecord = await _context.LicenceCertificationRecords.FindAsync(recordId);
            if (lcRecord == null) return NotFound();
            if (lcRecord.IsLocked)
            {
                TempData["Error"] = "This record is locked and cannot be modified.";
                TempData["ActiveTab"] = "dtab8";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            // Avoid duplicates
            var exists = await _context.LCDependencies
                .AnyAsync(d => d.LicenceCertificationRecordId == recordId
                            && d.RegulatoryApplicationId == regulatoryApplicationId);
            if (!exists)
            {
                var user = await _userManager.GetUserAsync(User);
                _context.LCDependencies.Add(new LCDependency
                {
                    LicenceCertificationRecordId = recordId,
                    RegulatoryApplicationId      = regulatoryApplicationId,
                    LinkedSince = DateTime.UtcNow,
                    Notes       = notes,
                    CreatedBy   = user?.UserName
                });

                _context.LCAuditLogs.Add(new LCAuditLog
                {
                    LicenceCertificationRecordId = recordId,
                    Action    = $"Dependency linked to RI application #{regulatoryApplicationId}",
                    Details   = notes,
                    UserId    = user?.Id.ToString(),
                    UserName  = user?.UserName,
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Dependency link added.";
            }
            else
            {
                TempData["Error"] = "This RegIntel application is already linked.";
            }

            TempData["ActiveTab"] = "dtab8";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // POST /LicencesCertifications/RemoveDependency
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> RemoveDependency(int dependencyId, int recordId)
        {
            var lcRecord = await _context.LicenceCertificationRecords.FindAsync(recordId);
            if (lcRecord != null && lcRecord.IsLocked)
            {
                TempData["Error"] = "This record is locked and cannot be modified.";
                TempData["ActiveTab"] = "dtab8";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            var dep = await _context.LCDependencies.FindAsync(dependencyId);
            if (dep != null)
            {
                _context.LCDependencies.Remove(dep);

                var user = await _userManager.GetUserAsync(User);
                _context.LCAuditLogs.Add(new LCAuditLog
                {
                    LicenceCertificationRecordId = recordId,
                    Action    = $"Dependency removed: RI application #{dep.RegulatoryApplicationId}",
                    UserId    = user?.Id.ToString(),
                    UserName  = user?.UserName,
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Dependency link removed.";
            }

            TempData["ActiveTab"] = "dtab8";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────

        private async Task FireTransitionNotification(
            LicenceCertificationRecord record, LCStatus newStatus, string actingUser)
        {
            if (newStatus is not (LCStatus.Expired or LCStatus.Suspended or
                                  LCStatus.Withdrawn or LCStatus.RenewalDue)) return;

            var (alertType, level) = newStatus switch
            {
                LCStatus.Expired    => ("ExpiryEscalation", "EXPIRED"),
                LCStatus.Suspended  => ("Suspension",       "CRITICAL"),
                LCStatus.Withdrawn  => ("Suspension",       "WARNING"),
                _                   => ("RenewalAlert",     "WARNING")   // RenewalDue
            };

            var recipients = new List<string> { actingUser };
            if (!string.IsNullOrEmpty(record.InternalResponsiblePersonId))
            {
                var resp = await _userManager.FindByIdAsync(record.InternalResponsiblePersonId);
                if (resp?.Email != null && !recipients.Contains(resp.Email))
                    recipients.Add(resp.Email);
            }

            await _notification.SendAsync(alertType, level, record.LCNumber,
                $"Record {record.LCNumber} transitioned to {newStatus}.", recipients);
        }

        private static string SanitiseFileName(string name)
        {
            // Replace spaces, strip non-alphanumeric (except . _ -), cap at 80 chars
            var safe = System.Text.RegularExpressions.Regex
                .Replace(name.Replace(' ', '_'), @"[^a-zA-Z0-9._\-]", "");
            return safe.Length > 80 ? safe[..80] : safe;
        }

        // ── AJAX: Get licence types for a category ────────────────────────────

        // GET /LicencesCertifications/GetLicenceTypes?category=1
        [HttpGet]
        public async Task<IActionResult> GetLicenceTypes(int category)
        {
            var cat = (LCCategory)category;

            var types = await _context.LCLicenceTypes
                .Where(t => t.IsActive && t.Category == cat)
                .OrderBy(t => t.DisplayOrder)
                .Select(t => new {
                    t.Id,
                    t.DisplayName,
                    t.DefaultRenewalLeadTimeDays,
                    t.RequiresExpiryDate,
                    t.IssuingBody
                })
                .ToListAsync();

            return Json(types);
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────

        private async Task<string> GenerateLCNumber()
        {
            int year = DateTime.UtcNow.Year;
            var prefix = $"LC-{year}-";

            int count = await _context.LicenceCertificationRecords
                .CountAsync(r => r.LCNumber.StartsWith(prefix));

            return $"{prefix}{(count + 1):D4}";
        }

        private async Task PopulateViewData(LicenceCertificationRecord? record)
        {
            // Users list — only those with regulatory module access
            var regulatoryRoles = new[] { "Administrator", "RegulatoryManager", "RegulatoryUser" };
            var regulatoryUsers = new List<AppUser>();
            foreach (var role in regulatoryRoles)
                regulatoryUsers.AddRange(await _userManager.GetUsersInRoleAsync(role));

            var users = regulatoryUsers
                .DistinctBy(u => u.Id)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .ToList();

            ViewBag.UsersList = new SelectList(
                users.Select(u => new { Id = u.Id.ToString(), Name = $"{u.FirstName} {u.LastName} ({u.UserName})".Trim() }),
                "Id", "Name",
                record?.InternalResponsiblePersonId);

            // Suppliers list for third-party records
            var suppliers = await _context.Supplier
                .OrderBy(s => s.LegalName)
                .ToListAsync();

            ViewBag.SuppliersList = new SelectList(
                suppliers.Select(s => new { s.Id, Name = s.LegalName }),
                "Id", "Name",
                record?.SupplierId);

            // Licence types — initially load all; JS cascade filters by category
            var types = await _context.LCLicenceTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayOrder)
                .ToListAsync();

            ViewBag.LicenceTypesList = new SelectList(types, "Id", "DisplayName", record?.LicenceTypeId);
        }

        // ── DASHBOARD ─────────────────────────────────────────────────────────

        public async Task<IActionResult> Dashboard()
        {
            var today = SiteTime.Today;

            var all = await _context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted)
                .Include(r => r.LicenceType)
                .Include(r => r.Conditions.Where(c => !c.IsDeleted))
                .ToListAsync();

            ViewBag.TotalRecords  = all.Count;
            ViewBag.ActiveCount   = all.Count(r => r.Status == LCStatus.IssuedActive || r.Status == LCStatus.ConditionsApplied);
            ViewBag.ConditionsCount = all.Count(r =>
                (r.Status == LCStatus.IssuedActive || r.Status == LCStatus.ConditionsApplied) &&
                r.Conditions.Any(c => c.Status == LCConditionStatus.Active));
            ViewBag.RenewalDueCount = all.Count(r => r.Status == LCStatus.RenewalDue);
            ViewBag.ExpiredCount  = all.Count(r => r.Status == LCStatus.Expired);
            ViewBag.SuspendedCount = all.Count(r => r.Status == LCStatus.Suspended);
            ViewBag.OpenConditionCount = all.Sum(r => r.Conditions.Count(c => c.Status == LCConditionStatus.Active));

            // Status breakdown for chart — use display names so labels read naturally
            var statusCounts = all
                .GroupBy(r => EnumHelper.Display(r.Status))
                .ToDictionary(g => g.Key, g => g.Count());
            ViewBag.StatusCounts = statusCounts;

            // Category breakdown
            var categoryCounts = all
                .GroupBy(r => r.Category.ToString())
                .ToDictionary(g => g.Key, g => g.Count());
            ViewBag.CategoryCounts = categoryCounts;

            // Expiring within the LC expiry warning window (System Settings)
            var lcWarnDays = (await _context.SystemSettings.FindAsync(1))?.LcExpiryWarningDays ?? 90;
            ViewBag.LcExpiryWarningDays = lcWarnDays;
            ViewBag.Expiring90 = all
                .Where(r => r.ExpiryDate.HasValue
                         && r.ExpiryDate.Value.Date >= today
                         && r.ExpiryDate.Value.Date <= today.AddDays(lcWarnDays)
                         && r.Status != LCStatus.Expired
                         && r.Status != LCStatus.Withdrawn)
                .OrderBy(r => r.ExpiryDate)
                .Take(10)
                .ToList();

            // Renewal due
            ViewBag.RenewalDue = all
                .Where(r => r.Status == LCStatus.RenewalDue)
                .OrderBy(r => r.ExpiryDate)
                .Take(10)
                .ToList();

            // Open conditions — typed tuple avoids dynamic in the view
            ViewBag.OpenConditions = all
                .SelectMany(r => r.Conditions
                    .Where(c => c.Status == LCConditionStatus.Active)
                    .Select(c => (Record: r, Condition: c)))
                .OrderBy(x => x.Condition.DueDate)
                .Take(10)
                .ToList();

            // ISO certs with upcoming surveillance audits (60 days)
            ViewBag.SurveillanceDue = all
                .Where(r => r.Category == LCCategory.ISOPrivateCertification
                         && r.NextSurveillanceAuditDate.HasValue
                         && r.NextSurveillanceAuditDate.Value.Date <= today.AddDays(60)
                         && r.Status != LCStatus.Expired
                         && r.Status != LCStatus.Withdrawn)
                .OrderBy(r => r.NextSurveillanceAuditDate)
                .Take(10)
                .ToList();

            // Overdue RFIs
            ViewBag.OverdueRFIs = all
                .Where(r => r.RFIResponseDueDate.HasValue
                         && r.RFIResponseDueDate.Value.Date < today
                         && r.RFIResponseSubmittedDate == null
                         && r.Status != LCStatus.Expired
                         && r.Status != LCStatus.Suspended
                         && r.Status != LCStatus.Withdrawn)
                .OrderBy(r => r.RFIResponseDueDate)
                .Take(10)
                .ToList();

            return View();
        }

        // ── CALENDAR ──────────────────────────────────────────────────────────

        public async Task<IActionResult> Calendar()
        {
            var today = SiteTime.Today;
            var windowEnd = today.AddDays(180);

            var records = await _context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted && r.Status != LCStatus.Withdrawn)
                .Include(r => r.Conditions.Where(c => !c.IsDeleted && c.Status == LCConditionStatus.Active))
                .ToListAsync();

            // Build flat event list for FullCalendar JSON
            var events = new List<object>();

            foreach (var r in records)
            {
                // Expiry date
                if (r.ExpiryDate.HasValue && r.ExpiryDate.Value.Date <= windowEnd)
                {
                    string color = r.ExpiryDate.Value.Date < today ? "#dc3545"
                                 : r.ExpiryDate.Value.Date <= today.AddDays(30) ? "#fd7e14"
                                 : r.ExpiryDate.Value.Date <= today.AddDays(90) ? "#ffc107"
                                 : "#198754";
                    events.Add(new {
                        id       = $"exp-{r.Id}",
                        title    = $"Expires: {r.LCNumber}",
                        start    = r.ExpiryDate.Value.ToString("yyyy-MM-dd"),
                        color,
                        url      = Url.Action("Details", new { id = r.Id }),
                        extendedProps = new { type = "Expiry", lcNumber = r.LCNumber }
                    });
                }

                // Renewal lead-time trigger date — show even if already past (overdue window)
                if (r.ExpiryDate.HasValue && r.RenewalLeadTimeDays > 0)
                {
                    var renewalStart = r.ExpiryDate.Value.Date.AddDays(-r.RenewalLeadTimeDays);
                    if (renewalStart <= windowEnd)
                    {
                        string renColor = renewalStart < today ? "#fd7e14" : "#0dcaf0";
                        events.Add(new {
                            id       = $"ren-{r.Id}",
                            title    = $"Renewal Start: {r.LCNumber}",
                            start    = renewalStart.ToString("yyyy-MM-dd"),
                            color    = renColor,
                            url      = Url.Action("Details", new { id = r.Id }),
                            extendedProps = new { type = "RenewalStart", lcNumber = r.LCNumber }
                        });
                    }
                }

                // ISO surveillance audit
                if (r.NextSurveillanceAuditDate.HasValue && r.NextSurveillanceAuditDate.Value.Date <= windowEnd)
                {
                    string color = r.NextSurveillanceAuditDate.Value.Date < today ? "#dc3545" : "#6610f2";
                    events.Add(new {
                        id       = $"iso-{r.Id}",
                        title    = $"ISO Audit: {r.LCNumber}",
                        start    = r.NextSurveillanceAuditDate.Value.ToString("yyyy-MM-dd"),
                        color,
                        url      = Url.Action("Details", new { id = r.Id }),
                        extendedProps = new { type = "SurveillanceAudit", lcNumber = r.LCNumber }
                    });
                }

                // RFI response due
                if (r.RFIResponseDueDate.HasValue && r.RFIResponseDueDate.Value.Date <= windowEnd
                    && r.RFIResponseSubmittedDate == null)
                {
                    string color = r.RFIResponseDueDate.Value.Date < today ? "#dc3545" : "#fd7e14";
                    events.Add(new {
                        id       = $"rfi-{r.Id}",
                        title    = $"RFI Due: {r.LCNumber}",
                        start    = r.RFIResponseDueDate.Value.ToString("yyyy-MM-dd"),
                        color,
                        url      = Url.Action("Details", new { id = r.Id }),
                        extendedProps = new { type = "RFIDue", lcNumber = r.LCNumber }
                    });
                }

                // Conditions
                foreach (var cond in r.Conditions)
                {
                    if (cond.DueDate.HasValue && cond.DueDate.Value.Date <= windowEnd)
                    {
                        string color = cond.DueDate.Value.Date < today ? "#dc3545" : "#20c997";
                        events.Add(new {
                            id       = $"cond-{cond.Id}",
                            title    = $"Condition: {r.LCNumber}",
                            start    = cond.DueDate.Value.ToString("yyyy-MM-dd"),
                            color,
                            url      = Url.Action("Details", new { id = r.Id }),
                            extendedProps = new { type = "Condition", lcNumber = r.LCNumber,
                                desc = cond.ConditionDescription.Length > 60
                                    ? cond.ConditionDescription[..60] + "…"
                                    : cond.ConditionDescription }
                        });
                    }
                }
            }

            ViewBag.CalendarEventsJson = System.Text.Json.JsonSerializer.Serialize(events);
            return View();
        }

        // ── EXPORT REGISTER ───────────────────────────────────────────────────

        public async Task<IActionResult> ExportRegister()
        {
            var records = await _context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted)
                .Include(r => r.LicenceType)
                .Include(r => r.Supplier)
                .Include(r => r.Conditions.Where(c => !c.IsDeleted))
                .Include(r => r.Documents.Where(d => !d.IsDeleted))
                .OrderBy(r => r.LCNumber)
                .ToListAsync();

            // Build user display-name lookup so exports show names, not GUIDs
            var userDisplayNames = (await _userManager.Users.ToListAsync())
                .ToDictionary(u => u.Id.ToString(), u => $"{u.FirstName} {u.LastName}".Trim());
            string ResponsibleName(string? uid) =>
                uid != null && userDisplayNames.TryGetValue(uid, out var n) && n.Length > 0 ? n : (uid ?? "");

            using var wb = new XLWorkbook();

            // Sheet 1: LC Register
            var ws = wb.Worksheets.Add("LC Register");
            var headers = new[]
            {
                "LC Number","Status","Category","Holder Type","Holder Name",
                "Licence Number","Jurisdiction","Licence Type","Issue Date","Expiry Date",
                "Renewal Lead Days","Next Renewal Due","Scope","Responsible Person",
                "Certification Body","Standard Version","Last Inspection","RFI Due Date",
                "Open Conditions","Documents","Created By","Created At"
            };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            int row = 2;
            var today = SiteTime.Today;
            foreach (var r in records)
            {
                ws.Cell(row,  1).Value = r.LCNumber;
                ws.Cell(row,  2).Value = r.Status.ToString();
                ws.Cell(row,  3).Value = r.Category.ToString();
                ws.Cell(row,  4).Value = r.HolderType.ToString();
                ws.Cell(row,  5).Value = r.HolderName ?? r.Supplier?.LegalName ?? "";
                ws.Cell(row,  6).Value = r.LicenceNumber ?? "";
                ws.Cell(row,  7).Value = r.LicenceType?.ApplicableJurisdiction ?? "";
                ws.Cell(row,  8).Value = r.LicenceType?.DisplayName ?? "";
                ws.Cell(row,  9).Value = r.IssueDate.HasValue ? r.IssueDate.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 10).Value = r.ExpiryDate.HasValue ? r.ExpiryDate.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 11).Value = r.RenewalLeadTimeDays > 0 ? r.RenewalLeadTimeDays.ToString() : "";
                ws.Cell(row, 12).Value = r.NextRenewalDue.HasValue ? r.NextRenewalDue.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 13).Value = r.Scope ?? "";
                ws.Cell(row, 14).Value = ResponsibleName(r.InternalResponsiblePersonId);
                ws.Cell(row, 15).Value = r.CertificationBody ?? "";
                ws.Cell(row, 16).Value = r.StandardVersion ?? "";
                ws.Cell(row, 17).Value = r.LastInspectionDate.HasValue ? r.LastInspectionDate.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 18).Value = r.RFIResponseDueDate.HasValue ? r.RFIResponseDueDate.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 19).Value = r.Conditions.Count(c => c.Status == LCConditionStatus.Active);
                ws.Cell(row, 20).Value = r.Documents.Count(d => !d.IsDeleted);
                ws.Cell(row, 21).Value = r.CreatedBy ?? "";
                ws.Cell(row, 22).Value = r.CreatedAt.ToSite().ToString("dd/MM/yyyy HH:mm");
                row++;
            }

            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#343a40");
            ws.Row(1).Style.Font.FontColor = XLColor.White;
            ws.Columns().AdjustToContents();

            // Sheet 2: Conditions — look up CAPA display IDs by PK
            var capaDisplayIds = await _context.Capa!
                .Where(c => c.CapaId != null)
                .ToDictionaryAsync(c => c.Id, c => c.CapaId!);

            var ws2 = wb.Worksheets.Add("Conditions");
            var condHeaders = new[] { "LC Number","Description","Status","Due Date","Completed At","CAPA ID","Completion Notes" };
            for (int i = 0; i < condHeaders.Length; i++)
                ws2.Cell(1, i + 1).Value = condHeaders[i];

            int crow = 2;
            foreach (var r in records)
            {
                foreach (var c in r.Conditions.OrderBy(c => c.CreatedAt))
                {
                    ws2.Cell(crow, 1).Value = r.LCNumber;
                    ws2.Cell(crow, 2).Value = c.ConditionDescription;
                    ws2.Cell(crow, 3).Value = c.Status.ToString();
                    ws2.Cell(crow, 4).Value = c.DueDate.HasValue ? c.DueDate.Value.ToString("dd/MM/yyyy") : "";
                    ws2.Cell(crow, 5).Value = c.CompletedAt.HasValue ? c.CompletedAt.Value.ToSite().ToString("dd/MM/yyyy") : "";
                    ws2.Cell(crow, 6).Value = c.LinkedCAPAId.HasValue && capaDisplayIds.TryGetValue(c.LinkedCAPAId.Value, out var cid) ? cid : "";
                    ws2.Cell(crow, 7).Value = c.CompletionNotes ?? "";
                    crow++;
                }
            }

            ws2.Row(1).Style.Font.Bold = true;
            ws2.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#343a40");
            ws2.Row(1).Style.Font.FontColor = XLColor.White;
            ws2.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            string fileName = $"LCRegister_{SiteTime.Today:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ── EXPIRY REPORT ─────────────────────────────────────────────────────

        public async Task<IActionResult> ExpiryReport(int days = 90)
        {
            var today    = SiteTime.Today;
            var cutoff   = today.AddDays(days);

            var records = await _context.LicenceCertificationRecords
                .Where(r => !r.IsDeleted
                         && r.ExpiryDate.HasValue
                         && r.ExpiryDate.Value.Date >= today
                         && r.ExpiryDate.Value.Date <= cutoff
                         && r.Status != LCStatus.Withdrawn
                         && r.Status != LCStatus.Expired)
                .Include(r => r.LicenceType)
                .OrderBy(r => r.ExpiryDate)
                .ToListAsync();

            // Build user display-name lookup so exports show names, not GUIDs
            var userDisplayNames = (await _userManager.Users.ToListAsync())
                .ToDictionary(u => u.Id.ToString(), u => $"{u.FirstName} {u.LastName}".Trim());
            string ResponsibleName(string? uid) =>
                uid != null && userDisplayNames.TryGetValue(uid, out var n) && n.Length > 0 ? n : (uid ?? "");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add($"Expiring Within {days} Days");
            var headers = new[]
            {
                "LC Number","Status","Category","Holder Name","Jurisdiction","Licence Type",
                "Expiry Date","Days To Expiry","Renewal Lead Days","Scope","Responsible Person"
            };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            int row = 2;
            foreach (var r in records)
            {
                int daysLeft = r.ExpiryDate.HasValue ? (r.ExpiryDate.Value.Date - today).Days : 0;

                ws.Cell(row,  1).Value = r.LCNumber;
                ws.Cell(row,  2).Value = r.Status.ToString();
                ws.Cell(row,  3).Value = r.Category.ToString();
                ws.Cell(row,  4).Value = r.HolderName ?? "";
                ws.Cell(row,  5).Value = r.LicenceType?.ApplicableJurisdiction ?? "";
                ws.Cell(row,  6).Value = r.LicenceType?.DisplayName ?? "";
                ws.Cell(row,  7).Value = r.ExpiryDate.HasValue ? r.ExpiryDate.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row,  8).Value = daysLeft;
                ws.Cell(row,  9).Value = r.RenewalLeadTimeDays > 0 ? r.RenewalLeadTimeDays.ToString() : "";
                ws.Cell(row, 10).Value = r.Scope ?? "";
                ws.Cell(row, 11).Value = ResponsibleName(r.InternalResponsiblePersonId);

                // Colour-code by urgency
                var fill = daysLeft < 0 ? XLColor.FromHtml("#f8d7da")
                         : daysLeft <= 30 ? XLColor.FromHtml("#ffe5d0")
                         : daysLeft <= 90 ? XLColor.FromHtml("#fff3cd")
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

            string fileName = $"LCExpiryReport_{days}d_{SiteTime.Today:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
