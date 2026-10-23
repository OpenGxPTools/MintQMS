using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.Navigation;
using OpenQMS.Models.ViewModels;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reports/ManagementReview
        public async Task<IActionResult> ManagementReview(DateTime? from, DateTime? to)
        {
            var today = SiteTime.Today;
            var fromDate = from ?? new DateTime(today.Year, 1, 1);
            var toDate = to ?? today;

            ViewBag.From = fromDate;
            ViewBag.To = toDate;

            // Use exclusive upper bound so records created any time on toDate are included
            var toExclusive = toDate.AddDays(1);

            // CAPAs
            var capas = await _context.Capa.Where(c => !c.IsDeleted).ToListAsync();
            ViewBag.CapaTotal = capas.Count;
            ViewBag.CapaOpenedInPeriod = capas.Count(c => c.DeterminedOn.ToSite() >= fromDate && c.DeterminedOn.ToSite() < toExclusive);
            ViewBag.CapaClosedInPeriod = capas.Count(c => c.ApprovedOn.HasValue && c.ApprovedOn.ToSite() >= fromDate && c.ApprovedOn.ToSite() < toExclusive);
            ViewBag.CapaCurrentlyOpen = capas.Count(c => c.Status != Capa.CapaStatus.Approved);
            ViewBag.CapaOverdue = capas.Count(c => c.Status != Capa.CapaStatus.Approved && c.TargetCompletionDate.HasValue && c.TargetCompletionDate < today);

            // Deviations
            var devs = await _context.Deviation.Where(d => !d.IsDeleted).ToListAsync();
            ViewBag.DevTotal = devs.Count;
            ViewBag.DevOpenedInPeriod = devs.Count(d => d.IdentifiedOn.ToSite() >= fromDate && d.IdentifiedOn.ToSite() < toExclusive);
            ViewBag.DevClosedInPeriod = devs.Count(d => d.ApprovedOn.HasValue && d.ApprovedOn.ToSite() >= fromDate && d.ApprovedOn.ToSite() < toExclusive);
            ViewBag.DevCurrentlyOpen = devs.Count(d => d.Status != Deviation.DeviationStatus.Approved);
            ViewBag.DevCritical = devs.Count(d => d.Severity == DeviationSeverity.Critical);
            ViewBag.DevMajor    = devs.Count(d => d.Severity == DeviationSeverity.Major);
            ViewBag.DevMinor    = devs.Count(d => d.Severity == DeviationSeverity.Minor);
            ViewBag.DevEventTypeCounts = Enum.GetValues<DeviationEventType>()
                .Select(et => (Label: et.ToString(), Count: devs.Count(d => d.EventType == et)))
                .Where(x => x.Count > 0)
                .OrderByDescending(x => x.Count)
                .ToList();

            // Changes
            var changes = await _context.Change.Where(c => !c.IsDeleted).ToListAsync();
            ViewBag.ChangeTotal = changes.Count;
            ViewBag.ChangeOpenedInPeriod = changes.Count(c => c.ProposedOn.ToSite() >= fromDate && c.ProposedOn.ToSite() < toExclusive);
            ViewBag.ChangeClosedInPeriod = changes.Count(c => c.ApprovedOn.HasValue && c.ApprovedOn.ToSite() >= fromDate && c.ApprovedOn.ToSite() < toExclusive);
            ViewBag.ChangeCurrentlyOpen = changes.Count(c => c.Status != Change.ChangeStatus.Approved);
            ViewBag.ChangeOverdue = changes.Count(c => c.Status != Change.ChangeStatus.Approved && c.TargetImplementationDate.HasValue && c.TargetImplementationDate < today);

            // Complaints
            var complaints = await _context.Complaint.Where(c => !c.IsDeleted).ToListAsync();
            ViewBag.ComplaintTotal = complaints.Count;
            ViewBag.ComplaintReceivedInPeriod = complaints.Count(c => c.ReceivedOn.ToSite() >= fromDate && c.ReceivedOn.ToSite() < toExclusive);
            ViewBag.ComplaintClosedInPeriod = complaints.Count(c => c.ClosedOn.HasValue && c.ClosedOn.ToSite() >= fromDate && c.ClosedOn.ToSite() < toExclusive);
            ViewBag.ComplaintCurrentlyOpen = complaints.Count(c => c.Status != Complaint.ComplaintStatus.Closed);
            ViewBag.ComplaintCritical = complaints.Count(c => c.Severity == Complaint.ComplaintSeverity.Critical);
            ViewBag.ComplaintMajor = complaints.Count(c => c.Severity == Complaint.ComplaintSeverity.Major);
            ViewBag.ComplaintMinor = complaints.Count(c => c.Severity == Complaint.ComplaintSeverity.Minor);

            // Audits — scoped to those planned within the period
            var audits = await _context.Audit.Where(a => !a.IsDeleted).ToListAsync();
            var auditsInPeriod = audits.Where(a => a.PlannedStartDate.HasValue && a.PlannedStartDate >= fromDate && a.PlannedStartDate < toExclusive).ToList();
            ViewBag.AuditScheduledInPeriod = auditsInPeriod.Count;
            ViewBag.AuditCompletedInPeriod = auditsInPeriod.Count(a => a.Status == Audit.AuditStatus.Closed || a.Status == Audit.AuditStatus.Approved);
            ViewBag.AuditCriticalFindings = auditsInPeriod.Sum(a => a.CriticalFindings);
            ViewBag.AuditMajorFindings = auditsInPeriod.Sum(a => a.MajorFindings);
            ViewBag.AuditMinorFindings = auditsInPeriod.Sum(a => a.MinorFindings);
            ViewBag.AuditCAPARequired = auditsInPeriod.Count(a => a.CAPARequired);

            // Risks — always show full register snapshot
            var risks = await _context.Risk.Where(r => !r.IsDeleted).ToListAsync();
            ViewBag.RiskTotal = risks.Count;
            ViewBag.RiskHigh = risks.Count(r => (r.ResidualRPN ?? r.InitialRPN) >= 80);
            ViewBag.RiskMedium = risks.Count(r => (r.ResidualRPN ?? r.InitialRPN) >= 40 && (r.ResidualRPN ?? r.InitialRPN) < 80);
            ViewBag.RiskLow = risks.Count(r => (r.ResidualRPN ?? r.InitialRPN) < 40);
            ViewBag.RiskOpen = risks.Count(r => r.Status != Risk.RiskStatus.Closed);
            ViewBag.RiskClosed = risks.Count(r => r.Status == Risk.RiskStatus.Closed);

            return View();
        }

        // GET: Reports/ExportManagementReview
        public async Task<IActionResult> ExportManagementReview(DateTime from, DateTime to)
        {
            var today = SiteTime.Today;
            var toExclusive = to.AddDays(1);

            var capas = await _context.Capa.Where(c => !c.IsDeleted).ToListAsync();
            var devs = await _context.Deviation.Where(d => !d.IsDeleted).ToListAsync();
            var changes = await _context.Change.Where(c => !c.IsDeleted).ToListAsync();
            var complaints = await _context.Complaint.Where(c => !c.IsDeleted).ToListAsync();
            var audits = await _context.Audit.Where(a => !a.IsDeleted).ToListAsync();
            var risks = await _context.Risk.Where(r => !r.IsDeleted).ToListAsync();

            var auditsInPeriod = audits
                .Where(a => a.PlannedStartDate.HasValue && a.PlannedStartDate >= from && a.PlannedStartDate < toExclusive)
                .ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Management Review");

            ws.Cell(1, 1).Value = "Management Review Input Report";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#00467F");
            ws.Cell(2, 1).Value = $"Period: {from:dd MMM yyyy} to {to:dd MMM yyyy}";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(3, 1).Value = $"Generated: {SiteTime.Now:dd MMM yyyy HH:mm} {SiteTime.Label}";
            ws.Cell(3, 1).Style.Font.Italic = true;

            int row = 5;

            void WriteSection(string title, (string Label, string Value)[] rows)
            {
                ws.Cell(row, 1).Value = title;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 12;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#00467F");
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
                ws.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#00467F");
                row++;
                foreach (var (label, value) in rows)
                {
                    ws.Cell(row, 1).Value = label;
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 2).Value = value;
                    row++;
                }
                row++;
            }

            WriteSection("CAPAs (Corrective & Preventive Actions)", new[]
            {
                ("Total CAPAs (all time)", capas.Count.ToString()),
                ("Opened in Period", capas.Count(c => c.DeterminedOn.ToSite() >= from && c.DeterminedOn.ToSite() < toExclusive).ToString()),
                ("Closed in Period", capas.Count(c => c.ApprovedOn.HasValue && c.ApprovedOn.ToSite() >= from && c.ApprovedOn.ToSite() < toExclusive).ToString()),
                ("Currently Open", capas.Count(c => c.Status != Capa.CapaStatus.Approved).ToString()),
                ("Overdue", capas.Count(c => c.Status != Capa.CapaStatus.Approved && c.TargetCompletionDate.HasValue && c.TargetCompletionDate < today).ToString())
            });

            var devEventRows = Enum.GetValues<DeviationEventType>()
                .Select(et => ($"  Event Type: {et}", devs.Count(d => d.EventType == et).ToString()))
                .ToArray();

            WriteSection("Deviations", new (string, string)[]
            {
                ("Total Deviations (all time)", devs.Count.ToString()),
                ("Identified in Period", devs.Count(d => d.IdentifiedOn.ToSite() >= from && d.IdentifiedOn.ToSite() < toExclusive).ToString()),
                ("Closed in Period", devs.Count(d => d.ApprovedOn.HasValue && d.ApprovedOn.ToSite() >= from && d.ApprovedOn.ToSite() < toExclusive).ToString()),
                ("Currently Open", devs.Count(d => d.Status != Deviation.DeviationStatus.Approved).ToString()),
                ("Severity: Critical (all time)", devs.Count(d => d.Severity == DeviationSeverity.Critical).ToString()),
                ("Severity: Major (all time)",    devs.Count(d => d.Severity == DeviationSeverity.Major).ToString()),
                ("Severity: Minor (all time)",    devs.Count(d => d.Severity == DeviationSeverity.Minor).ToString()),
            }.Concat(devEventRows).ToArray());

            WriteSection("Changes", new[]
            {
                ("Total Changes (all time)", changes.Count.ToString()),
                ("Proposed in Period", changes.Count(c => c.ProposedOn.ToSite() >= from && c.ProposedOn.ToSite() < toExclusive).ToString()),
                ("Approved in Period", changes.Count(c => c.ApprovedOn.HasValue && c.ApprovedOn.ToSite() >= from && c.ApprovedOn.ToSite() < toExclusive).ToString()),
                ("Currently Open", changes.Count(c => c.Status != Change.ChangeStatus.Approved).ToString()),
                ("Overdue", changes.Count(c => c.Status != Change.ChangeStatus.Approved && c.TargetImplementationDate.HasValue && c.TargetImplementationDate < today).ToString())
            });

            WriteSection("Complaints", new[]
            {
                ("Total Complaints (all time)", complaints.Count.ToString()),
                ("Received in Period", complaints.Count(c => c.ReceivedOn.ToSite() >= from && c.ReceivedOn.ToSite() < toExclusive).ToString()),
                ("Closed in Period", complaints.Count(c => c.ClosedOn.HasValue && c.ClosedOn.ToSite() >= from && c.ClosedOn.ToSite() < toExclusive).ToString()),
                ("Currently Open", complaints.Count(c => c.Status != Complaint.ComplaintStatus.Closed).ToString()),
                ("Critical Severity (all time)", complaints.Count(c => c.Severity == Complaint.ComplaintSeverity.Critical).ToString()),
                ("Major Severity (all time)", complaints.Count(c => c.Severity == Complaint.ComplaintSeverity.Major).ToString()),
                ("Minor Severity (all time)", complaints.Count(c => c.Severity == Complaint.ComplaintSeverity.Minor).ToString())
            });

            WriteSection("Audits (planned start within period)", new[]
            {
                ("Scheduled in Period", auditsInPeriod.Count.ToString()),
                ("Completed in Period", auditsInPeriod.Count(a => a.Status == Audit.AuditStatus.Closed || a.Status == Audit.AuditStatus.Approved).ToString()),
                ("Total Critical Findings", auditsInPeriod.Sum(a => a.CriticalFindings).ToString()),
                ("Total Major Findings", auditsInPeriod.Sum(a => a.MajorFindings).ToString()),
                ("Total Minor Findings", auditsInPeriod.Sum(a => a.MinorFindings).ToString()),
                ("Requiring CAPA", auditsInPeriod.Count(a => a.CAPARequired).ToString())
            });

            WriteSection("Risks (current register snapshot)", new[]
            {
                ("Total Risks", risks.Count.ToString()),
                ("High RPN (≥80)", risks.Count(r => (r.ResidualRPN ?? r.InitialRPN) >= 80).ToString()),
                ("Medium RPN (40–79)", risks.Count(r => (r.ResidualRPN ?? r.InitialRPN) >= 40 && (r.ResidualRPN ?? r.InitialRPN) < 80).ToString()),
                ("Low RPN (<40)", risks.Count(r => (r.ResidualRPN ?? r.InitialRPN) < 40).ToString()),
                ("Currently Open", risks.Count(r => r.Status != Risk.RiskStatus.Closed).ToString()),
                ("Closed", risks.Count(r => r.Status == Risk.RiskStatus.Closed).ToString())
            });

            ws.Column(1).Width = 38;
            ws.Column(2).Width = 20;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ManagementReview_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");
        }

        // GET: Reports/TrainingCompliance
        public async Task<IActionResult> TrainingCompliance(DateTime? from, DateTime? to)
        {
            var today = SiteTime.Today;
            var fromDate = from ?? new DateTime(today.Year, 1, 1);
            var toDate = to ?? today;

            ViewBag.From = fromDate;
            ViewBag.To = toDate;

            var trainings = await _context.Training
                .Include(t => t.Trainees)
                    .ThenInclude(ut => ut.Trainee)
                .Where(t => t.DueDate >= fromDate && t.DueDate < toDate.AddDays(1))
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            ViewBag.TotalTrainings = trainings.Count;
            ViewBag.CompletedTrainings = trainings.Count(t => t.Status == Training.TrainingStatus.Completed);
            ViewBag.CompliancePct = trainings.Count > 0
                ? (int)Math.Round(trainings.Count(t => t.Status == Training.TrainingStatus.Completed) * 100.0 / trainings.Count)
                : 0;
            ViewBag.Trainings = trainings;

            return View();
        }

        // GET: Reports/ExportTrainingCompliance
        public async Task<IActionResult> ExportTrainingCompliance(DateTime from, DateTime to)
        {
            var trainings = await _context.Training
                .Include(t => t.Trainees)
                    .ThenInclude(ut => ut.Trainee)
                .Where(t => t.DueDate >= from && t.DueDate < to.AddDays(1))
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            using var wb = new XLWorkbook();

            // Sheet 1: By Session
            var ws1 = wb.Worksheets.Add("By Session");
            ws1.Cell(1, 1).Value = "Training Compliance Report — By Session";
            ws1.Cell(1, 1).Style.Font.Bold = true;
            ws1.Cell(1, 1).Style.Font.FontSize = 14;
            ws1.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#00467F");
            ws1.Cell(2, 1).Value = $"Period: {from:dd MMM yyyy} to {to:dd MMM yyyy}";
            ws1.Cell(3, 1).Value = $"Generated: {SiteTime.Now:dd MMM yyyy HH:mm} {SiteTime.Label}";

            var headers1 = new[] { "Training ID", "Name", "Due Date", "Status", "No. of Attendees" };
            for (int i = 0; i < headers1.Length; i++)
            {
                var cell = ws1.Cell(5, i + 1);
                cell.Value = headers1[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#00467F");
                cell.Style.Font.FontColor = XLColor.White;
            }
            int r1 = 6;
            foreach (var t in trainings)
            {
                ws1.Cell(r1, 1).Value = t.TrainingId;
                ws1.Cell(r1, 2).Value = t.Name;
                ws1.Cell(r1, 3).Value = t.DueDate?.ToString("dd MMM yyyy") ?? "-";
                ws1.Cell(r1, 4).Value = t.Status.ToString();
                ws1.Cell(r1, 5).Value = t.Trainees?.Count ?? 0;
                if (t.Status != Training.TrainingStatus.Completed)
                    ws1.Row(r1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3F3");
                r1++;
            }
            ws1.Columns().AdjustToContents(10, 60);

            // Sheet 2: By User
            var ws2 = wb.Worksheets.Add("By User");
            ws2.Cell(1, 1).Value = "Training Compliance Report — By User";
            ws2.Cell(1, 1).Style.Font.Bold = true;
            ws2.Cell(1, 1).Style.Font.FontSize = 14;
            ws2.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#00467F");
            ws2.Cell(2, 1).Value = $"Period: {from:dd MMM yyyy} to {to:dd MMM yyyy}";
            ws2.Cell(3, 1).Value = $"Generated: {SiteTime.Now:dd MMM yyyy HH:mm} {SiteTime.Label}";

            var headers2 = new[] { "Name", "Email", "Sessions Attended", "Sessions Completed", "Compliance %" };
            for (int i = 0; i < headers2.Length; i++)
            {
                var cell = ws2.Cell(5, i + 1);
                cell.Value = headers2[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#00467F");
                cell.Style.Font.FontColor = XLColor.White;
            }

            var userStats = trainings
                .SelectMany(t => t.Trainees
                    .Select(ut => new { ut.Trainee, TrainingStatus = t.Status }))
                .GroupBy(x => x.Trainee.Email)
                .Select(g => new
                {
                    Name = $"{g.First().Trainee.FirstName} {g.First().Trainee.LastName}".Trim(),
                    Email = g.Key,
                    Total = g.Count(),
                    Completed = g.Count(x => x.TrainingStatus == Training.TrainingStatus.Completed)
                })
                .OrderBy(x => x.Name)
                .ToList();

            int r2 = 6;
            foreach (var u in userStats)
            {
                var pct = u.Total > 0 ? (int)Math.Round(u.Completed * 100.0 / u.Total) : 0;
                ws2.Cell(r2, 1).Value = u.Name;
                ws2.Cell(r2, 2).Value = u.Email;
                ws2.Cell(r2, 3).Value = u.Total;
                ws2.Cell(r2, 4).Value = u.Completed;
                ws2.Cell(r2, 5).Value = $"{pct}%";
                if (pct < 100)
                    ws2.Row(r2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3F3");
                r2++;
            }
            ws2.Columns().AdjustToContents(10, 60);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"TrainingCompliance_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");
        }

        // GET: Reports/OverdueItems
        public async Task<IActionResult> OverdueItems()
        {
            var today = SiteTime.Today;
            var thirtyDaysAgo = today.AddDays(-30);

            var overdueCapas = await _context.Capa
                .Where(c => !c.IsDeleted
                    && c.Status != Capa.CapaStatus.Approved
                    && c.TargetCompletionDate.HasValue
                    && c.TargetCompletionDate < today)
                .OrderBy(c => c.TargetCompletionDate)
                .ToListAsync();

            var overdueChanges = await _context.Change
                .Where(c => !c.IsDeleted
                    && c.Status != Change.ChangeStatus.Approved
                    && c.TargetImplementationDate.HasValue
                    && c.TargetImplementationDate < today)
                .OrderBy(c => c.TargetImplementationDate)
                .ToListAsync();

            var overdueAudits = await _context.Audit
                .Where(a => !a.IsDeleted
                    && a.PlannedEndDate.HasValue
                    && a.PlannedEndDate < today
                    && a.Status != Audit.AuditStatus.Closed
                    && a.Status != Audit.AuditStatus.Approved)
                .OrderBy(a => a.PlannedEndDate)
                .ToListAsync();

            var overdueRisks = await _context.Risk
                .Where(r => !r.IsDeleted
                    && r.Status != Risk.RiskStatus.Closed
                    && r.TargetCompletionDate.HasValue
                    && r.TargetCompletionDate < today)
                .OrderBy(r => r.TargetCompletionDate)
                .ToListAsync();

            var overdueDeviations = await _context.Deviation
                .Where(d => !d.IsDeleted
                    && d.Status != Deviation.DeviationStatus.Approved
                    && d.IdentifiedOn < thirtyDaysAgo)
                .OrderBy(d => d.IdentifiedOn)
                .ToListAsync();

            var overdueComplaints = await _context.Complaint
                .Where(c => !c.IsDeleted
                    && c.Status != Complaint.ComplaintStatus.Closed
                    && c.ReceivedOn < thirtyDaysAgo)
                .OrderBy(c => c.ReceivedOn)
                .ToListAsync();

            var overdueDocReviews = await _context.AppDocument
                .Where(d => !d.IsDeleted && !d.IsLocked
                    && d.Status == AppDocument.DocumentStatus.Approved
                    && d.ReviewDueDate.HasValue
                    && d.ReviewDueDate.Value.Date < today)
                .OrderBy(d => d.ReviewDueDate)
                .ToListAsync();

            var dueSoonDocReviews = await _context.AppDocument
                .Where(d => !d.IsDeleted && !d.IsLocked
                    && d.Status == AppDocument.DocumentStatus.Approved
                    && d.ReviewDueDate.HasValue
                    && d.ReviewDueDate.Value.Date >= today
                    && d.ReviewDueDate.Value.Date <= today.AddDays(30))
                .OrderBy(d => d.ReviewDueDate)
                .ToListAsync();

            ViewBag.OverdueCapas = overdueCapas;
            ViewBag.OverdueChanges = overdueChanges;
            ViewBag.OverdueAudits = overdueAudits;
            ViewBag.OverdueRisks = overdueRisks;
            ViewBag.OverdueDeviations = overdueDeviations;
            ViewBag.OverdueComplaints = overdueComplaints;
            ViewBag.OverdueDocReviews = overdueDocReviews;
            ViewBag.DueSoonDocReviews = dueSoonDocReviews;
            ViewBag.TotalOverdue = overdueCapas.Count + overdueChanges.Count + overdueAudits.Count
                + overdueRisks.Count + overdueDeviations.Count + overdueComplaints.Count
                + overdueDocReviews.Count;
            ViewBag.Today = today;

            return View();
        }

        // GET: Reports/SupportingDocumentExpiry
        public async Task<IActionResult> SupportingDocumentExpiry()
        {
            var today = SiteTime.Today;
            var vm = new SupportingDocExpiryViewModel { GeneratedAt = DateTime.UtcNow };

            // ── Products ─────────────────────────────────────────────────────
            var productDocs = await _context.ProductDocument
                .Include(d => d.Product)
                .Where(d => d.ExpiryDate.HasValue && d.Product != null && !d.Product.IsDeleted)
                .OrderBy(d => d.Product!.ProductId)
                .ThenBy(d => d.ExpiryDate)
                .ToListAsync();

            if (productDocs.Any())
            {
                var module = new SupportingDocModuleGroup { ModuleName = "Products", Controller = "Products" };
                foreach (var grp in productDocs.GroupBy(d => d.ProductId))
                {
                    var parent = grp.First().Product!;
                    var pg = new SupportingDocParentGroup
                    {
                        ParentId = parent.Id,
                        ParentIdentifier = parent.ProductId,
                        ParentName = parent.Name
                    };
                    foreach (var d in grp)
                    {
                        var expiry = d.ExpiryDate!.Value.Date;
                        pg.Documents.Add(new SupportingDocItem
                        {
                            Id = d.Id,
                            FileName = d.FileName,
                            Description = d.Description,
                            ExpiryDate = d.ExpiryDate,
                            ReminderDays = d.ReminderDays,
                            IsExpired = expiry < today,
                            IsExpiringSoon = expiry >= today && expiry <= today.AddDays(d.ReminderDays)
                        });
                    }
                    module.Parents.Add(pg);
                }
                vm.Modules.Add(module);
            }

            // ── Materials ─────────────────────────────────────────────────────
            var materialDocs = await _context.MaterialDocument
                .Include(d => d.Material)
                .Where(d => d.ExpiryDate.HasValue && d.Material != null && !d.Material.IsDeleted)
                .OrderBy(d => d.Material!.MaterialId)
                .ThenBy(d => d.ExpiryDate)
                .ToListAsync();

            if (materialDocs.Any())
            {
                var module = new SupportingDocModuleGroup { ModuleName = "Materials", Controller = "Materials" };
                foreach (var grp in materialDocs.GroupBy(d => d.MaterialId))
                {
                    var parent = grp.First().Material!;
                    var pg = new SupportingDocParentGroup
                    {
                        ParentId = parent.Id,
                        ParentIdentifier = parent.MaterialId,
                        ParentName = parent.Name
                    };
                    foreach (var d in grp)
                    {
                        var expiry = d.ExpiryDate!.Value.Date;
                        pg.Documents.Add(new SupportingDocItem
                        {
                            Id = d.Id,
                            FileName = d.FileName,
                            Description = d.Description,
                            ExpiryDate = d.ExpiryDate,
                            ReminderDays = d.ReminderDays,
                            IsExpired = expiry < today,
                            IsExpiringSoon = expiry >= today && expiry <= today.AddDays(d.ReminderDays)
                        });
                    }
                    module.Parents.Add(pg);
                }
                vm.Modules.Add(module);
            }

            // ── Processes ─────────────────────────────────────────────────────
            var processDocs = await _context.ProcessDocument
                .Include(d => d.Process)
                .Where(d => d.ExpiryDate.HasValue && d.Process != null && !d.Process.IsDeleted)
                .OrderBy(d => d.Process!.ProcessId)
                .ThenBy(d => d.ExpiryDate)
                .ToListAsync();

            if (processDocs.Any())
            {
                var module = new SupportingDocModuleGroup { ModuleName = "Processes", Controller = "Processes" };
                foreach (var grp in processDocs.GroupBy(d => d.ProcessId))
                {
                    var parent = grp.First().Process!;
                    var pg = new SupportingDocParentGroup
                    {
                        ParentId = parent.Id,
                        ParentIdentifier = parent.ProcessId,
                        ParentName = parent.Name
                    };
                    foreach (var d in grp)
                    {
                        var expiry = d.ExpiryDate!.Value.Date;
                        pg.Documents.Add(new SupportingDocItem
                        {
                            Id = d.Id,
                            FileName = d.FileName,
                            Description = d.Description,
                            ExpiryDate = d.ExpiryDate,
                            ReminderDays = d.ReminderDays,
                            IsExpired = expiry < today,
                            IsExpiringSoon = expiry >= today && expiry <= today.AddDays(d.ReminderDays)
                        });
                    }
                    module.Parents.Add(pg);
                }
                vm.Modules.Add(module);
            }

            // ── Assets ────────────────────────────────────────────────────────
            var assetDocs = await _context.AssetDocument
                .Include(d => d.Asset)
                .Where(d => d.ExpiryDate.HasValue && d.Asset != null && !d.Asset.IsDeleted)
                .OrderBy(d => d.Asset!.AssetId)
                .ThenBy(d => d.ExpiryDate)
                .ToListAsync();

            if (assetDocs.Any())
            {
                var module = new SupportingDocModuleGroup { ModuleName = "Assets", Controller = "Assets" };
                foreach (var grp in assetDocs.GroupBy(d => d.AssetId))
                {
                    var parent = grp.First().Asset!;
                    var pg = new SupportingDocParentGroup
                    {
                        ParentId = parent.Id,
                        ParentIdentifier = parent.AssetId,
                        ParentName = parent.Name
                    };
                    foreach (var d in grp)
                    {
                        var expiry = d.ExpiryDate!.Value.Date;
                        pg.Documents.Add(new SupportingDocItem
                        {
                            Id = d.Id,
                            FileName = d.FileName,
                            Description = d.Description,
                            ExpiryDate = d.ExpiryDate,
                            ReminderDays = d.ReminderDays,
                            IsExpired = expiry < today,
                            IsExpiringSoon = expiry >= today && expiry <= today.AddDays(d.ReminderDays)
                        });
                    }
                    module.Parents.Add(pg);
                }
                vm.Modules.Add(module);
            }

            // ── Services ──────────────────────────────────────────────────────
            var serviceDocs = await _context.ServiceDocument
                .Include(d => d.Service)
                .Where(d => d.ExpiryDate.HasValue && d.Service != null && !d.Service.IsDeleted)
                .OrderBy(d => d.Service!.ServiceId)
                .ThenBy(d => d.ExpiryDate)
                .ToListAsync();

            if (serviceDocs.Any())
            {
                var module = new SupportingDocModuleGroup { ModuleName = "Services", Controller = "Services" };
                foreach (var grp in serviceDocs.GroupBy(d => d.ServiceId))
                {
                    var parent = grp.First().Service!;
                    var pg = new SupportingDocParentGroup
                    {
                        ParentId = parent.Id,
                        ParentIdentifier = parent.ServiceId,
                        ParentName = parent.Name
                    };
                    foreach (var d in grp)
                    {
                        var expiry = d.ExpiryDate!.Value.Date;
                        pg.Documents.Add(new SupportingDocItem
                        {
                            Id = d.Id,
                            FileName = d.FileName,
                            Description = d.Description,
                            ExpiryDate = d.ExpiryDate,
                            ReminderDays = d.ReminderDays,
                            IsExpired = expiry < today,
                            IsExpiringSoon = expiry >= today && expiry <= today.AddDays(d.ReminderDays)
                        });
                    }
                    module.Parents.Add(pg);
                }
                vm.Modules.Add(module);
            }

            // ── Customers ─────────────────────────────────────────────────────
            var customerDocs = await _context.CustomerDocument
                .Include(d => d.Customer)
                .Where(d => d.ExpiryDate.HasValue && d.Customer != null && !d.Customer.IsDeleted)
                .OrderBy(d => d.Customer!.CustomerId)
                .ThenBy(d => d.ExpiryDate)
                .ToListAsync();

            if (customerDocs.Any())
            {
                var module = new SupportingDocModuleGroup { ModuleName = "Customers", Controller = "Customers" };
                foreach (var grp in customerDocs.GroupBy(d => d.CustomerId))
                {
                    var parent = grp.First().Customer!;
                    var pg = new SupportingDocParentGroup
                    {
                        ParentId = parent.Id,
                        ParentIdentifier = parent.CustomerId,
                        ParentName = parent.LegalName
                    };
                    foreach (var d in grp)
                    {
                        var expiry = d.ExpiryDate!.Value.Date;
                        pg.Documents.Add(new SupportingDocItem
                        {
                            Id = d.Id,
                            FileName = d.FileName,
                            Description = d.Description,
                            ExpiryDate = d.ExpiryDate,
                            ReminderDays = d.ReminderDays,
                            IsExpired = expiry < today,
                            IsExpiringSoon = expiry >= today && expiry <= today.AddDays(d.ReminderDays)
                        });
                    }
                    module.Parents.Add(pg);
                }
                vm.Modules.Add(module);
            }

            // ── Suppliers ─────────────────────────────────────────────────────
            var supplierDocs = await _context.SupplierDocument
                .Include(d => d.Supplier)
                .Where(d => d.ExpiryDate.HasValue && d.Supplier != null && !d.Supplier.IsDeleted)
                .OrderBy(d => d.Supplier!.SupplierCode)
                .ThenBy(d => d.ExpiryDate)
                .ToListAsync();

            if (supplierDocs.Any())
            {
                var module = new SupportingDocModuleGroup { ModuleName = "Suppliers", Controller = "Suppliers" };
                foreach (var grp in supplierDocs.GroupBy(d => d.SupplierId))
                {
                    var parent = grp.First().Supplier!;
                    var pg = new SupportingDocParentGroup
                    {
                        ParentId = parent.Id,
                        ParentIdentifier = parent.SupplierCode,
                        ParentName = parent.LegalName
                    };
                    foreach (var d in grp)
                    {
                        var expiry = d.ExpiryDate!.Value.Date;
                        pg.Documents.Add(new SupportingDocItem
                        {
                            Id = d.Id,
                            FileName = d.FileName,
                            Description = d.Description,
                            ExpiryDate = d.ExpiryDate,
                            ReminderDays = d.ReminderDays,
                            IsExpired = expiry < today,
                            IsExpiringSoon = expiry >= today && expiry <= today.AddDays(d.ReminderDays)
                        });
                    }
                    module.Parents.Add(pg);
                }
                vm.Modules.Add(module);
            }

            return View(vm);
        }
    }
}
