using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class PharmacovigilanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly PVWorkflowService _workflow;
        private readonly PVRegulatoryService _regulatory;
        private readonly CIOMSFormService _cioms;

        public PharmacovigilanceController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            IPasswordHasher<AppUser> passwordHasher,
            PVWorkflowService workflow,
            PVRegulatoryService regulatory,
            CIOMSFormService cioms)
        {
            _context = context;
            _userManager = userManager;
            _passwordHasher = passwordHasher;
            _workflow = workflow;
            _regulatory = regulatory;
            _cioms = cioms;
        }

        // GET: Pharmacovigilance/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            // KPI cards
            var allOpen = await _context.AdverseEvent
                .Where(a => a.Status != AdverseEvent.AEStatus.Closed &&
                            a.Status != AdverseEvent.AEStatus.Voided)
                .ToListAsync();

            ViewBag.TotalOpen       = allOpen.Count;
            ViewBag.SeriousCount    = allOpen.Count(a => a.IsSerious);
            ViewBag.OverdueCount    = await _regulatory.GetOverdueCases().CountAsync();
            ViewBag.SubmittedMonth  = await _context.AERegulatoryReport
                .CountAsync(r => r.TGASubmissionDate.HasValue &&
                                 r.TGASubmissionDate.Value >= monthStart);

            // Deadline panels
            ViewBag.OverdueCases      = await _regulatory.GetOverdueCases()
                .OrderBy(a => a.TGADeadlineDate).ToListAsync();
            ViewBag.ApproachingCases  = await _regulatory.GetApproachingDeadlineCases(10)
                .OrderBy(a => a.TGADeadlineDate).ToListAsync();

            // Status chart (bar)
            var statusCounts = await _context.AdverseEvent
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.StatusLabels = System.Text.Json.JsonSerializer.Serialize(
                statusCounts.Select(s => s.Status.ToString()).ToList());
            ViewBag.StatusData = System.Text.Json.JsonSerializer.Serialize(
                statusCounts.Select(s => s.Count).ToList());

            // Serious vs Non-Serious doughnut
            var totalAe = await _context.AdverseEvent.CountAsync(a => a.Status != AdverseEvent.AEStatus.Voided);
            var seriousTotal = await _context.AdverseEvent.CountAsync(a => a.IsSerious && a.Status != AdverseEvent.AEStatus.Voided);
            ViewBag.SeriousTotal    = seriousTotal;
            ViewBag.NonSeriousTotal = totalAe - seriousTotal;

            // Signal flags
            ViewBag.SignalFlags = await _context.AdverseEvent
                .Where(a => a.SignalDetected &&
                            a.Status != AdverseEvent.AEStatus.Closed &&
                            a.Status != AdverseEvent.AEStatus.Voided)
                .OrderByDescending(a => a.Id)
                .Take(10)
                .ToListAsync();

            // Recent activity
            ViewBag.RecentActivity = await _context.AEAuditLog
                .Include(l => l.AdverseEvent)
                .OrderByDescending(l => l.PerformedOn)
                .Take(10)
                .ToListAsync();

            return View();
        }

        // GET: Pharmacovigilance
        public async Task<IActionResult> Index()
        {
            var aes = await _context.AdverseEvent
                .OrderByDescending(a => a.Id)
                .ToListAsync();
            return View(aes);
        }

        // GET: Pharmacovigilance/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var ae = await _context.AdverseEvent
                .Include(a => a.Reporter)
                .Include(a => a.Patient)
                .Include(a => a.SuspectProduct).ThenInclude(s => s!.Product)
                .Include(a => a.RegulatoryReport)
                .Include(a => a.Attachments!.Where(att => !att.IsDeleted))
                .Include(a => a.ParentAE)
                .Include(a => a.FollowUpReports)
                .Include(a => a.LinkedComplaint)
                .Include(a => a.LinkedCapa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ae == null) return NotFound();

            ViewData["AuditLog"] = await _context.AEAuditLog
                .Where(l => l.AdverseEventId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            ViewData["DeadlineStatus"] = _regulatory.GetDeadlineStatus(ae);

            // Open complaints available to link (only offered when this case isn't already linked)
            if (ae.LinkedComplaintId == null && !ae.IsLocked && ae.Status != AdverseEvent.AEStatus.Voided)
            {
                ViewData["AvailableComplaints"] = await _context.Complaint
                    .Where(c => !c.IsDeleted && c.Status != Complaint.ComplaintStatus.Closed
                             && c.LinkedAEId == null
                             && !_context.AdverseEvent.Any(a => a.LinkedComplaintId == c.Id))
                    .OrderByDescending(c => c.Id)
                    .Select(c => new { c.Id, Label = c.ComplaintId + " — " + c.Title })
                    .ToListAsync();
            }

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.AEAttachment.DeletedOnly().Where(f => f.AdverseEventId == id).ToListAsync();
            return View(ae);
        }

        // POST: Pharmacovigilance/LinkComplaint — associate this case with an existing complaint
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> LinkComplaint(int aeId, int complaintId)
        {
            var ae = await _context.AdverseEvent.FindAsync(aeId);
            if (ae == null) return NotFound();

            if (ae.IsLocked || ae.Status == AdverseEvent.AEStatus.Voided)
            {
                TempData["Error"] = "This case is locked and cannot be modified.";
                return RedirectToAction(nameof(Details), new { id = aeId });
            }

            if (ae.LinkedComplaintId.HasValue && ae.LinkedComplaintId != complaintId)
            {
                TempData["Error"] = "This adverse event is already linked to another complaint. Unlink it first.";
                return RedirectToAction(nameof(Details), new { id = aeId });
            }

            var complaint = await _context.Complaint.FindAsync(complaintId);
            if (complaint == null || complaint.IsDeleted)
            {
                TempData["Error"] = "Selected complaint not found.";
                return RedirectToAction(nameof(Details), new { id = aeId });
            }

            // One-to-one: a complaint may be linked to at most one adverse event.
            var complaintTaken = (complaint.LinkedAEId.HasValue && complaint.LinkedAEId != aeId)
                || await _context.AdverseEvent.AnyAsync(a => a.LinkedComplaintId == complaintId && a.Id != aeId);
            if (complaintTaken)
            {
                TempData["Error"] = "That complaint is already linked to another adverse event.";
                return RedirectToAction(nameof(Details), new { id = aeId });
            }

            ae.LinkedComplaintId = complaintId;
            complaint.LinkedAEId = ae.Id;

            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = aeId,
                Action      = "ComplaintLinked",
                NewValue    = complaint.ComplaintId,
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Linked to complaint {complaint.ComplaintId}.";
            return RedirectToAction(nameof(Details), new { id = aeId });
        }

        // POST: Pharmacovigilance/UnlinkComplaint — remove the complaint association
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UnlinkComplaint(int aeId)
        {
            var ae = await _context.AdverseEvent.FindAsync(aeId);
            if (ae == null) return NotFound();

            if (ae.IsLocked || ae.Status == AdverseEvent.AEStatus.Voided)
            {
                TempData["Error"] = "This case is locked and cannot be modified.";
                return RedirectToAction(nameof(Details), new { id = aeId });
            }

            var complaintId = ae.LinkedComplaintId;
            ae.LinkedComplaintId = null;

            string? unlinkedComplaintNumber = null;
            if (complaintId.HasValue)
            {
                var complaint = await _context.Complaint.FindAsync(complaintId.Value);
                if (complaint != null)
                {
                    unlinkedComplaintNumber = complaint.ComplaintId;
                    if (complaint.LinkedAEId == aeId)
                    {
                        // Promote another still-linked case to "primary", or clear it
                        complaint.LinkedAEId = await _context.AdverseEvent
                            .Where(a => a.LinkedComplaintId == complaintId.Value && a.Id != aeId)
                            .Select(a => (int?)a.Id)
                            .FirstOrDefaultAsync();
                    }
                }
            }

            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = aeId,
                Action      = "ComplaintUnlinked",
                OldValue    = unlinkedComplaintNumber,
                PerformedBy = _userManager.GetUserName(User) ?? "Unknown",
                PerformedOn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Complaint link removed.";
            return RedirectToAction(nameof(Details), new { id = aeId });
        }

        // GET: Pharmacovigilance/Create
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public IActionResult Create(int? complaintId)
        {
            ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && p.Status == Product.ProductStatus.Approved).OrderBy(p => p.Name), "Id", "Name");

            var ae = new AdverseEvent
            {
                Reporter = new AEReporter(),
                Patient = new AEPatient(),
                SuspectProduct = new AESuspectProduct(),
                RegulatoryReport = new AERegulatoryReport()
            };

            if (complaintId.HasValue)
            {
                var complaint = _context.Complaint
                    .Include(c => c.Product)
                    .FirstOrDefault(c => c.Id == complaintId.Value);
                if (complaint != null)
                {
                    // One-to-one: refuse to raise a second adverse event for an already-linked complaint.
                    var complaintTaken = complaint.LinkedAEId.HasValue
                        || _context.AdverseEvent.Any(a => a.LinkedComplaintId == complaint.Id);
                    if (complaintTaken)
                    {
                        TempData["Error"] = "This complaint is already linked to an adverse event.";
                        return RedirectToAction("Details", "Complaints", new { id = complaint.Id });
                    }

                    ae.LinkedComplaintId = complaint.Id;
                    ae.AEDescription     = $"Raised from Complaint {complaint.ComplaintId}: {complaint.Title}\n\n{complaint.Description}";
                    ae.ReportSource      = AdverseEvent.AEReportSource.Spontaneous;
                    if (complaint.ProductId.HasValue)
                        ae.SuspectProduct.ProductId = complaint.ProductId;
                    ViewBag.FromComplaint = complaint.ComplaintId;
                }
            }

            return View(ae);
        }

        // POST: Pharmacovigilance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Create(
            AdverseEvent ae,
            AEReporter reporter,
            AEPatient patient,
            AESuspectProduct suspectProduct,
            AERegulatoryReport regulatoryReport)
        {
            ModelState.Remove("Reporter");
            ModelState.Remove("Patient");
            ModelState.Remove("SuspectProduct");
            ModelState.Remove("RegulatoryReport");
            ModelState.Remove("Attachments");
            ModelState.Remove("ParentAE");
            ModelState.Remove("FollowUpReports");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("AENumber");
            ModelState.Remove("reporter.AdverseEvent");
            ModelState.Remove("patient.AdverseEvent");
            ModelState.Remove("suspectProduct.AdverseEvent");
            ModelState.Remove("suspectProduct.Product");
            ModelState.Remove("regulatoryReport.AdverseEvent");
            ModelState.Remove("regulatoryReport.PSUR");
            ModelState.Remove("LinkedComplaint");
            ModelState.Remove("LinkedCapa");

            if (ModelState.IsValid)
            {
                // Generate AE Number
                var year = DateTime.UtcNow.Year;
                var lastAE = await _context.AdverseEvent
                    .Where(a => a.AENumber.StartsWith($"AE-{year}-"))
                    .OrderByDescending(a => a.Id)
                    .FirstOrDefaultAsync();
                var seq = 1;
                if (lastAE != null)
                {
                    var parts = lastAE.AENumber.Split('-');
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int prevSeq))
                        seq = prevSeq + 1;
                }
                ae.AENumber = $"AE-{year}-{seq:D4}";
                ae.ReportDate = DateTime.UtcNow;
                ae.CreatedBy = _userManager.GetUserName(User) ?? "Unknown";
                ae.CreatedOn = DateTime.UtcNow;
                ae.Status = AdverseEvent.AEStatus.Draft;
                ae.IsLocked = false;

                // Compute derived seriousness fields
                ae.IsSerious = ae.SeriousnessCriteria != AdverseEvent.SeriousnessFlags.None;
                ae.IsFatalOrLifeThreatening =
                    ae.SeriousnessCriteria.HasFlag(AdverseEvent.SeriousnessFlags.Death) ||
                    ae.SeriousnessCriteria.HasFlag(AdverseEvent.SeriousnessFlags.LifeThreatening);

                if (ae.Day0Date.HasValue)
                {
                    var days = ae.IsFatalOrLifeThreatening ? 7 : 15;
                    ae.TGADeadlineDate = ae.Day0Date.Value.AddDays(days);
                }

                _context.Add(ae);
                await _context.SaveChangesAsync();

                // Save child records
                reporter.AdverseEventId = ae.Id;
                _context.AEReporter.Add(reporter);

                patient.AdverseEventId = ae.Id;
                _context.AEPatient.Add(patient);

                suspectProduct.AdverseEventId = ae.Id;
                _context.AESuspectProduct.Add(suspectProduct);

                regulatoryReport.AdverseEventId = ae.Id;
                if (regulatoryReport.Status == default)
                    regulatoryReport.Status = AERegulatoryReport.AERegulatoryStatus.PendingSubmission;
                _context.AERegulatoryReport.Add(regulatoryReport);

                _context.AEAuditLog.Add(new AEAuditLog
                {
                    AdverseEventId = ae.Id,
                    Action = "Created",
                    NewValue = ae.AENumber,
                    PerformedBy = ae.CreatedBy,
                    PerformedOn = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                // Write-back: stamp the source complaint with this AE's Id (one-to-one)
                if (ae.LinkedComplaintId.HasValue)
                {
                    var sourceComplaint = await _context.Complaint.FindAsync(ae.LinkedComplaintId.Value);
                    var complaintFree = sourceComplaint != null && sourceComplaint.LinkedAEId == null
                        && !await _context.AdverseEvent.AnyAsync(a => a.LinkedComplaintId == sourceComplaint.Id && a.Id != ae.Id);
                    if (complaintFree)
                    {
                        sourceComplaint!.LinkedAEId = ae.Id;
                    }
                    else
                    {
                        // Complaint already linked — preserve one-to-one by leaving this case unlinked.
                        ae.LinkedComplaintId = null;
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"Adverse Event {ae.AENumber} created successfully.";
                return RedirectToAction(nameof(Details), new { id = ae.Id });
            }

            ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && (p.Status == Product.ProductStatus.Approved || p.Id == suspectProduct.ProductId)).OrderBy(p => p.Name), "Id", "Name", suspectProduct.ProductId);
            ae.Reporter = reporter;
            ae.Patient = patient;
            ae.SuspectProduct = suspectProduct;
            ae.RegulatoryReport = regulatoryReport;
            return View(ae);
        }

        // GET: Pharmacovigilance/Edit/5
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var ae = await _context.AdverseEvent
                .Include(a => a.Reporter)
                .Include(a => a.Patient)
                .Include(a => a.SuspectProduct)
                .Include(a => a.RegulatoryReport)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (ae == null) return NotFound();

            // Ensure child navigation objects exist for the form
            ae.Reporter ??= new AEReporter { AdverseEventId = ae.Id };
            ae.Patient ??= new AEPatient { AdverseEventId = ae.Id };
            ae.SuspectProduct ??= new AESuspectProduct { AdverseEventId = ae.Id };
            ae.RegulatoryReport ??= new AERegulatoryReport { AdverseEventId = ae.Id };

            ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && (p.Status == Product.ProductStatus.Approved || p.Id == ae.SuspectProduct.ProductId)).OrderBy(p => p.Name), "Id", "Name", ae.SuspectProduct.ProductId);
            return View(ae);
        }

        // POST: Pharmacovigilance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(
            int id,
            AdverseEvent ae,
            AEReporter reporter,
            AEPatient patient,
            AESuspectProduct suspectProduct,
            AERegulatoryReport regulatoryReport)
        {
            if (id != ae.Id) return NotFound();

            ModelState.Remove("Reporter");
            ModelState.Remove("Patient");
            ModelState.Remove("SuspectProduct");
            ModelState.Remove("RegulatoryReport");
            ModelState.Remove("Attachments");
            ModelState.Remove("ParentAE");
            ModelState.Remove("FollowUpReports");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("reporter.AdverseEvent");
            ModelState.Remove("patient.AdverseEvent");
            ModelState.Remove("suspectProduct.AdverseEvent");
            ModelState.Remove("suspectProduct.Product");
            ModelState.Remove("regulatoryReport.AdverseEvent");
            ModelState.Remove("regulatoryReport.PSUR");

            if (ModelState.IsValid)
            {
                var existing = await _context.AdverseEvent
                    .Include(a => a.Reporter)
                    .Include(a => a.Patient)
                    .Include(a => a.SuspectProduct)
                    .Include(a => a.RegulatoryReport)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (existing == null) return NotFound();

                if (existing.IsLocked)
                {
                    TempData["Error"] = "This record is locked and cannot be edited.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Recalculate derived fields
                ae.IsSerious = ae.SeriousnessCriteria != AdverseEvent.SeriousnessFlags.None;
                ae.IsFatalOrLifeThreatening =
                    ae.SeriousnessCriteria.HasFlag(AdverseEvent.SeriousnessFlags.Death) ||
                    ae.SeriousnessCriteria.HasFlag(AdverseEvent.SeriousnessFlags.LifeThreatening);
                if (ae.Day0Date.HasValue)
                {
                    var days = ae.IsFatalOrLifeThreatening ? 7 : 15;
                    ae.TGADeadlineDate = ae.Day0Date.Value.AddDays(days);
                }

                // Preserve immutable fields
                ae.AENumber = existing.AENumber;
                ae.ReportDate = existing.ReportDate;
                ae.CreatedBy = existing.CreatedBy;
                ae.CreatedOn = existing.CreatedOn;
                ae.Status = existing.Status;
                ae.IsLocked = existing.IsLocked;
                ae.ModifiedBy = _userManager.GetUserName(User) ?? "Unknown";
                ae.ModifiedOn = DateTime.UtcNow;

                // Field-level change tracking for the audit log (existing = old values, ae = new)
                var diff = new List<string>();
                AuditDiffHelper.TrackDate(diff, "Day 0 Date",           existing.Day0Date,           ae.Day0Date);
                AuditDiffHelper.TrackEnum(diff, "Report Type",          existing.ReportType,         ae.ReportType);
                AuditDiffHelper.TrackEnum(diff, "Report Source",        existing.ReportSource,       ae.ReportSource);
                AuditDiffHelper.Track(diff,     "SAS Reference",        existing.SASReferenceNumber, ae.SASReferenceNumber);
                AuditDiffHelper.TrackEnum(diff, "Seriousness Criteria", existing.SeriousnessCriteria, ae.SeriousnessCriteria);
                AuditDiffHelper.Track(diff,     "Reportable",           existing.IsReportable,       ae.IsReportable);
                AuditDiffHelper.Track(diff,     "AE Description",       existing.AEDescription,      ae.AEDescription);
                AuditDiffHelper.Track(diff,     "MedDRA PT",            existing.MedDRAPT,           ae.MedDRAPT);
                AuditDiffHelper.Track(diff,     "MedDRA SOC",           existing.MedDRASOC,          ae.MedDRASOC);
                AuditDiffHelper.TrackDate(diff, "AE Start Date",        existing.AEStartDate,        ae.AEStartDate);
                AuditDiffHelper.TrackDate(diff, "AE End Date",          existing.AEEndDate,          ae.AEEndDate);
                AuditDiffHelper.TrackEnum(diff, "Outcome",              existing.Outcome,            ae.Outcome);
                AuditDiffHelper.TrackEnum(diff, "Causality",            existing.Causality,          ae.Causality);
                AuditDiffHelper.TrackEnum(diff, "Severity",             existing.Severity,           ae.Severity);
                AuditDiffHelper.TrackEnum(diff, "Expectedness",         existing.Expectedness,       ae.Expectedness);
                AuditDiffHelper.Track(diff,     "Medical Narrative",    existing.MedicalNarrative,   ae.MedicalNarrative);
                AuditDiffHelper.Track(diff,     "Medical Evaluator",    existing.MedicalEvaluator,   ae.MedicalEvaluator);
                AuditDiffHelper.TrackDate(diff, "Evaluation Date",      existing.EvaluationDate,     ae.EvaluationDate);
                AuditDiffHelper.Track<bool>(diff, "Signal Detected",     existing.SignalDetected,     ae.SignalDetected);

                // Child records are edited on the same form but are not AE scalar fields — diff them too
                // (compared before UpdateOrCreateChild overwrites the existing values below).
                AuditDiffHelper.TrackObject(diff, "Reporter",        existing.Reporter,         reporter,         "Id", "AdverseEventId");
                AuditDiffHelper.TrackObject(diff, "Patient",         existing.Patient,          patient,          "Id", "AdverseEventId");
                AuditDiffHelper.TrackObject(diff, "Suspect Product", existing.SuspectProduct,   suspectProduct,   "Id", "AdverseEventId");
                AuditDiffHelper.TrackObject(diff, "Reg. Report",     existing.RegulatoryReport, regulatoryReport, "Id", "AdverseEventId");
                var editNotes = AuditDiffHelper.Build(diff);

                _context.Entry(existing).CurrentValues.SetValues(ae);

                // Update or create child records
                UpdateOrCreateChild(existing.Reporter, reporter, r => { r.AdverseEventId = id; _context.AEReporter.Add(r); });
                UpdateOrCreateChild(existing.Patient, patient, p => { p.AdverseEventId = id; _context.AEPatient.Add(p); });
                UpdateOrCreateChild(existing.SuspectProduct, suspectProduct, s => { s.AdverseEventId = id; _context.AESuspectProduct.Add(s); });
                UpdateOrCreateChild(existing.RegulatoryReport, regulatoryReport, r => { r.AdverseEventId = id; _context.AERegulatoryReport.Add(r); });

                if (diff.Count > 0)
                    _context.AEAuditLog.Add(new AEAuditLog
                    {
                        AdverseEventId = id,
                        Action = "FieldEdit",
                        NewValue = $"Updated by {ae.ModifiedBy}",
                        Notes = editNotes,
                        PerformedBy = ae.ModifiedBy ?? "Unknown",
                        PerformedOn = DateTime.UtcNow
                    });

                await _context.SaveChangesAsync();

                TempData["Success"] = "Adverse Event updated successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["ProductId"] = new SelectList(_context.Product.Where(p => !p.IsDeleted && (p.Status == Product.ProductStatus.Approved || p.Id == suspectProduct.ProductId)).OrderBy(p => p.Name), "Id", "Name", suspectProduct.ProductId);
            ae.Reporter = reporter;
            ae.Patient = patient;
            ae.SuspectProduct = suspectProduct;
            ae.RegulatoryReport = regulatoryReport;
            return View(ae);
        }

        private void UpdateOrCreateChild<T>(T? existing, T incoming, Action<T> onCreate) where T : class
        {
            if (existing != null)
            {
                var entry = _context.Entry(existing);
                // The edit form round-trips each child's own Id but NOT its owning AdverseEventId,
                // so incoming carries AdverseEventId = 0. A blanket SetValues would overwrite the
                // existing owner FK with 0 and fail the foreign key on save. Preserve the primary key
                // and the AdverseEvent owner FK across the copy; user-editable FKs (e.g. ProductId,
                // PSURId) still update from the form.
                var preserve = entry.Metadata.FindPrimaryKey()!.Properties
                    .Select(p => p.Name)
                    .Concat(entry.Metadata.GetForeignKeys()
                        .Where(fk => fk.PrincipalEntityType.ClrType == typeof(AdverseEvent))
                        .SelectMany(fk => fk.Properties).Select(p => p.Name))
                    .Distinct()
                    .ToDictionary(n => n, n => entry.Property(n).CurrentValue);

                entry.CurrentValues.SetValues(incoming);

                foreach (var kv in preserve)
                    entry.Property(kv.Key).CurrentValue = kv.Value;
            }
            else
                onCreate(incoming);
        }

        // GET: Pharmacovigilance/Delete/5
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var ae = await _context.AdverseEvent.FirstOrDefaultAsync(m => m.Id == id);
            if (ae == null) return NotFound();
            return View(ae);
        }

        // POST: Pharmacovigilance/Delete/5
        // Voiding is a Draft-only soft-delete; allow the same roles that create and
        // transition cases (consistent with Edit/Reject/Close), not Administrator only.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ae = await _context.AdverseEvent.FindAsync(id);
            if (ae != null)
            {
                var oldStatus = ae.Status.ToString();
                ae.Status = AdverseEvent.AEStatus.Voided;
                _context.Update(ae);
                _context.AEAuditLog.Add(new AEAuditLog
                {
                    AdverseEventId = ae.Id,
                    Action = "Voided",
                    OldValue = oldStatus,
                    NewValue = AdverseEvent.AEStatus.Voided.ToString(),
                    PerformedBy = User.Identity?.Name ?? "Unknown",
                    PerformedOn = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Adverse Event {ae.AENumber} voided.";
            }
            return RedirectToAction(nameof(Index));
        }

        // AJAX: GET /Pharmacovigilance/CalculateDeadline?day0Date=2026-03-01&isFatalOrLifeThreatening=false
        [HttpGet]
        public IActionResult CalculateDeadline(string day0Date, bool isFatalOrLifeThreatening = false)
        {
            if (!DateTime.TryParse(day0Date, out var day0))
                return Json(new { error = "Invalid date" });

            var days = isFatalOrLifeThreatening ? 7 : 15;
            var deadline = day0.AddDays(days);
            var daysRemaining = (deadline.Date - SiteTime.Today).Days;
            var statusClass = daysRemaining < 0 ? "danger" :
                              daysRemaining <= 2 ? "danger" :
                              daysRemaining <= 5 ? "warning" : "success";

            return Json(new
            {
                deadline = deadline.ToString("yyyy-MM-dd"),
                deadlineFormatted = deadline.ToString("dd MMM yyyy"),
                daysRemaining,
                statusClass,
                label = daysRemaining < 0 ? $"OVERDUE ({Math.Abs(daysRemaining)} days)" :
                        daysRemaining == 0 ? "Due TODAY" :
                        $"{daysRemaining} day{(daysRemaining == 1 ? "" : "s")} remaining"
            });
        }

        // GET: Pharmacovigilance/Attachments/5
        public async Task<IActionResult> Attachments(int? id)
        {
            if (id == null) return NotFound();
            var ae = await _context.AdverseEvent
                .Include(a => a.Attachments!.Where(att => !att.IsDeleted))
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ae == null) return NotFound();
            return View(ae);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> UploadAttachment(int aeId, IFormFile file, string? documentType, string? description)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToDetailsAttachments(aeId);
            }

            var allowedExtensions = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not allowed. Accepted: PDF, Word, Excel, ZIP, JPG, PNG.";
                return RedirectToDetailsAttachments(aeId);
            }
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File size exceeds the 25 MB limit.";
                return RedirectToDetailsAttachments(aeId);
            }

            var ae = await _context.AdverseEvent.FindAsync(aeId);
            if (ae == null) return NotFound();

            if (ae.IsLocked)
            {
                TempData["Error"] = "This case is locked (finalised) and cannot be modified — attachments cannot be added.";
                return RedirectToDetailsAttachments(aeId);
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "pharmacovigilance", aeId.ToString());
            Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            _context.AEAttachment.Add(new AEAttachment
            {
                AdverseEventId = aeId,
                FileName = file.FileName,
                FilePath = $"/uploads/pharmacovigilance/{aeId}/{uniqueFileName}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                FileHash = fileHash,
                UploadedBy = User.Identity?.Name ?? "Unknown",
                UploadedDate = DateTime.UtcNow,
                DocumentType = documentType,
                Description = description
            });
            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = aeId,
                Action = "AttachmentUploaded",
                NewValue = file.FileName,
                Notes = string.IsNullOrWhiteSpace(description) ? null : $"Description: {description}",
                PerformedBy = User.Identity?.Name ?? "Unknown",
                PerformedOn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"File '{file.FileName}' uploaded successfully.";
            return RedirectToDetailsAttachments(aeId);
        }

        public async Task<IActionResult> DownloadAttachment(int? id, bool deleted = false)
        {
            if (id == null) return NotFound();
            var attachment = await _context.AEAttachment.ForDownload(deleted).FirstOrDefaultAsync(a => a.Id == id);
            if (attachment == null) return NotFound();
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToDetailsAttachments(attachment.AdverseEventId);
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                _context.AEAuditLog.Add(new AEAuditLog { AdverseEventId = attachment.AdverseEventId, Action = "IntegrityCheckFailed", OldValue = attachment.FileName, Notes = OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName), PerformedBy = User.Identity?.Name ?? "Unknown", PerformedOn = DateTime.UtcNow });
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                return RedirectToDetailsAttachments(attachment.AdverseEventId);
            }
            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = attachment.AdverseEventId,
                Action         = (deleted ? "Deleted attachment downloaded" : "Attachment downloaded"),
                Notes          = attachment.FileName,
                PerformedBy    = User.Identity?.Name ?? "Unknown",
                PerformedOn    = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.RegulatoryAttachment, null, "Pharmacovigilance",
                attachment.AdverseEventId.ToString(), null,
                User.Identity?.Name ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(attachment.FileName), wmCtx);
            return File(watermarked, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        public async Task<IActionResult> ViewAttachment(int? id)
        {
            if (id == null) return NotFound();
            var attachment = await _context.AEAttachment.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (attachment == null) return NotFound();
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToDetailsAttachments(attachment.AdverseEventId);
            }

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(filePath, attachment.FileHash))
            {
                _context.AEAuditLog.Add(new AEAuditLog { AdverseEventId = attachment.AdverseEventId, Action = "IntegrityCheckFailed", OldValue = attachment.FileName, Notes = OpenQMS.Services.FileIntegrity.AuditNote(attachment.FileName), PerformedBy = User.Identity?.Name ?? "Unknown", PerformedOn = DateTime.UtcNow });
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(attachment.FileName);
                return RedirectToDetailsAttachments(attachment.AdverseEventId);
            }
            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = attachment.AdverseEventId,
                Action         = "Attachment viewed",
                Notes          = attachment.FileName,
                PerformedBy    = User.Identity?.Name ?? "Unknown",
                PerformedOn    = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
                await stream.CopyToAsync(memory);
            memory.Position = 0;
            return File(memory, attachment.ContentType ?? "application/octet-stream");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> DeleteAttachment(int id, int aeId, string? deletionReason)
        {
            var attachment = await _context.AEAttachment.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (attachment != null)
            {
                // Resolve the parent from the attachment itself — never trust the posted id.
                aeId = attachment.AdverseEventId;

                var ae = await _context.AdverseEvent.FindAsync(aeId);
                if (ae != null && ae.IsLocked)
                {
                    TempData["Error"] = "This case is locked (finalised) and cannot be modified — attachments cannot be removed.";
                    return RedirectToDetailsAttachments(aeId);
                }

                if (string.IsNullOrWhiteSpace(deletionReason))
                {
                    TempData["Error"] = "A reason is required to delete an attachment.";
                    return RedirectToDetailsAttachments(aeId);
                }

                attachment.IsDeleted      = true;
                attachment.DeletedBy      = User.Identity?.Name ?? "Unknown";
                attachment.DeletedOn      = DateTime.UtcNow;
                attachment.DeletionReason = deletionReason.Trim();
                _context.AEAuditLog.Add(new AEAuditLog
                {
                    AdverseEventId = aeId,
                    Action = "AttachmentDeleted",
                    OldValue = attachment.FileName,
                    Notes = $"Reason: {attachment.DeletionReason}",
                    PerformedBy = User.Identity?.Name ?? "Unknown",
                    PerformedOn = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attachment deleted.";
            }
            return RedirectToDetailsAttachments(aeId);
        }

        // Attachments are managed inline on the Details page (Documents tab), mirroring the
        // RegIntel and Licences/Certifications modules. This re-opens that tab after an action.
        private IActionResult RedirectToDetailsAttachments(int aeId)
        {
            TempData["ActiveTab"] = "tab-attachments";
            return RedirectToAction(nameof(Details), new { id = aeId });
        }

        // ── Workflow Actions ──────────────────────────────────────────────────

        // POST: Submit for Medical Review (Draft → UnderMedicalReview)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> SubmitForReview(int id, string email, string pwd)
            => await TransitionWithCredentials(id, email, pwd, AdverseEvent.AEStatus.UnderMedicalReview, null);

        // POST: Complete Evaluation (UnderMedicalReview → PendingApproval)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CompleteEvaluation(int id, string email, string pwd)
            => await TransitionWithCredentials(id, email, pwd, AdverseEvent.AEStatus.PendingApproval, null);

        // POST: Send Back to Draft
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> SendBack(int id, string email, string pwd, string? comment)
            => await TransitionWithCredentials(id, email, pwd, AdverseEvent.AEStatus.Draft, comment);

        // POST: Approve (PendingApproval → Approved + lock)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Approve(int id, string email, string pwd)
            => await TransitionWithCredentials(id, email, pwd, AdverseEvent.AEStatus.Approved, null);

        // POST: Reject back to Medical Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Reject(int id, string email, string pwd, string? comment)
            => await TransitionWithCredentials(id, email, pwd, AdverseEvent.AEStatus.UnderMedicalReview, comment);

        // POST: Close Case (Approved → Closed)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CloseCase(int id, string email, string pwd)
            => await TransitionWithCredentials(id, email, pwd, AdverseEvent.AEStatus.Closed, null);

        // POST: Create Follow-up Report
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> CreateFollowUp(int id, string email, string pwd)
        {
            // E-signature must be made with the signed-in user's own credentials
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user = null;
            if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, pwd) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Invalid credentials.";
                return RedirectToAction(nameof(Details), new { id, isUserVerified = false });
            }

            var ae = await _context.AdverseEvent.FindAsync(id);
            if (ae == null) return NotFound();

            if (ae.Status != AdverseEvent.AEStatus.Approved && ae.Status != AdverseEvent.AEStatus.Closed)
            {
                TempData["Error"] = "Follow-up reports can only be created from Approved or Closed cases.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var followUp = await _workflow.CreateFollowUp(ae, _userManager.GetUserName(User) ?? email);
            TempData["Success"] = $"Follow-up report {followUp.AENumber} created.";
            return RedirectToAction(nameof(Details), new { id = followUp.Id });
        }

        // POST: Pharmacovigilance/LogTGASubmission
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> LogTGASubmission(
            int id, string submissionDate, string? adrsRef, string? method)
        {
            if (!DateTime.TryParse(submissionDate, out var parsedDate))
            {
                TempData["Error"] = "Invalid submission date.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userId = _userManager.GetUserName(User) ?? User.Identity?.Name ?? "Unknown";
            await _regulatory.LogTGASubmission(id, parsedDate, adrsRef, method, userId);

            TempData["Success"] = "TGA submission recorded successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Pharmacovigilance/ExportCIOMS/5
        [HttpGet]
        public async Task<IActionResult> ExportCIOMS(int id)
        {
            var ae = await _context.AdverseEvent
                .Include(a => a.Reporter)
                .Include(a => a.Patient)
                .Include(a => a.SuspectProduct)
                .Include(a => a.RegulatoryReport)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (ae == null) return NotFound();

            var pdfBytes = _cioms.GenerateCIOMS(ae, ae.Reporter, ae.Patient, ae.SuspectProduct);
            var fileName = $"CIOMS-I_{ae.AENumber.Replace("/", "-")}_{SiteTime.Now:yyyyMMdd}.pdf";

            _context.AEAuditLog.Add(new AEAuditLog
            {
                AdverseEventId = ae.Id,
                Action = "CIOMSExported",
                NewValue = fileName,
                PerformedBy = User.Identity?.Name ?? "Unknown",
                PerformedOn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task<IActionResult> TransitionWithCredentials(
            int id, string email, string pwd,
            AdverseEvent.AEStatus newStatus,
            string? comment)
        {
            // E-signature must be made with the signed-in user's own credentials
            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user = null;
            if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, pwd) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Invalid credentials.";
                return RedirectToAction(nameof(Details), new { id, isUserVerified = false });
            }

            var ae = await _context.AdverseEvent.FindAsync(id);
            if (ae == null) return NotFound();

            var validation = await _workflow.ValidateTransition(ae, newStatus);
            if (!validation.Success)
            {
                TempData["Error"] = validation.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.ApplyTransition(ae, newStatus, _userManager.GetUserName(User) ?? email, comment);

            TempData["Success"] = $"Status updated to: {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            var product = await _context.Product
                .Include(p => p.Strengths)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null) return Json(null);
            return Json(new
            {
                name          = product.Name,
                brandName     = product.BrandName,
                artgReference = product.ArtgReference
            });
        }
    }
}
