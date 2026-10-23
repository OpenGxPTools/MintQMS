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

using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.LC;
using OpenQMS.Models.RI;
using OpenQMS.Models.ViewModels;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new HomeViewModel();
            var today = SiteTime.Today;
            var cutoff90 = today.AddDays(90);

            // ── Current user for My Items ─────────────────────────────────
            var currentUserName = _userManager.GetUserName(User) ?? string.Empty;
            var currentUser     = await _userManager.GetUserAsync(User);

            // ── My Items ──────────────────────────────────────────────────

            if (_context.Capa != null)
            {
                vm.MyCAPAs = await _context.Capa
                    .CountAsync(c => !c.IsDeleted
                                  && c.DeterminedBy == currentUserName
                                  && c.Status != Capa.CapaStatus.Approved);

                vm.MyRecentCAPAs = (await _context.Capa
                    .Where(c => !c.IsDeleted
                             && c.DeterminedBy == currentUserName
                             && c.Status != Capa.CapaStatus.Approved)
                    .OrderByDescending(c => c.DeterminedOn)
                    .Take(5).ToListAsync())
                    .Select(c => new HomeActivityItem
                    {
                        Id = c.Id, RecordId = c.CapaId, Title = c.Title,
                        Status = c.Status switch
                        {
                            Capa.CapaStatus.PendingAssessment => "Pending Assessment",
                            Capa.CapaStatus.PendingApproval   => "Pending Approval",
                            _ => EnumHelper.Display(c.Status)
                        },
                        BadgeClass = c.Status switch
                        {
                            Capa.CapaStatus.Determination  => "primary",
                            Capa.CapaStatus.Assessment     => "warning",
                            Capa.CapaStatus.Accepted       => "info",
                            Capa.CapaStatus.Implementation => "info",
                            _ => "secondary"
                        },
                        Date = c.DeterminedOn
                    }).ToList();
            }

            if (_context.Deviation != null)
            {
                vm.MyDeviations = await _context.Deviation
                    .CountAsync(d => !d.IsDeleted
                                  && d.IdentifiedBy == currentUserName
                                  && d.Status != Deviation.DeviationStatus.Approved);

                vm.MyRecentDeviations = (await _context.Deviation
                    .Where(d => !d.IsDeleted
                             && d.IdentifiedBy == currentUserName
                             && d.Status != Deviation.DeviationStatus.Approved)
                    .OrderByDescending(d => d.IdentifiedOn)
                    .Take(5).ToListAsync())
                    .Select(d => new HomeActivityItem
                    {
                        Id = d.Id, RecordId = d.DeviationId, Title = d.Title,
                        Status = d.Status switch
                        {
                            Deviation.DeviationStatus.PendingAcceptance => "Pending Acceptance",
                            _ => EnumHelper.Display(d.Status)
                        },
                        BadgeClass = d.Status switch
                        {
                            Deviation.DeviationStatus.Identification    => "primary",
                            Deviation.DeviationStatus.Evaluation        => "info",
                            Deviation.DeviationStatus.PendingAcceptance => "warning",
                            Deviation.DeviationStatus.Accepted          => "info",
                            Deviation.DeviationStatus.Resolution        => "warning",
                            _ => "secondary"
                        },
                        Date = d.IdentifiedOn
                    }).ToList();
            }

            vm.MyDocuments = await _context.AppDocument
                .CountAsync(d => !d.IsDeleted
                              && d.AuthoredBy == currentUserName
                              && (d.Status == AppDocument.DocumentStatus.Draft
                               || d.Status == AppDocument.DocumentStatus.UnderReview));

            vm.MyRecentDocuments = (await _context.AppDocument
                .Where(d => !d.IsDeleted
                         && d.AuthoredBy == currentUserName
                         && (d.Status == AppDocument.DocumentStatus.Draft
                          || d.Status == AppDocument.DocumentStatus.UnderReview))
                .OrderByDescending(d => d.AuthoredOn)
                .Take(5).ToListAsync())
                .Select(d => new HomeActivityItem
                {
                    Id = d.Id, RecordId = d.DocumentId, Title = d.Title,
                    Status = EnumHelper.Display(d.Status),
                    BadgeClass = d.Status switch
                    {
                        AppDocument.DocumentStatus.Draft       => "primary",
                        AppDocument.DocumentStatus.UnderReview => "warning",
                        _ => "secondary"
                    },
                    Date = d.AuthoredOn
                }).ToList();

            if (currentUser != null)
            {
                vm.MyTrainings = await _context.UserTraining
                    .CountAsync(ut => ut.TraineeId == currentUser.Id
                                   && ut.Training.Status == Training.TrainingStatus.Scheduled);

                vm.MyRecentTrainings = (await _context.UserTraining
                    .Where(ut => ut.TraineeId == currentUser.Id
                              && ut.Training.Status == Training.TrainingStatus.Scheduled)
                    .Include(ut => ut.Training)
                    .OrderBy(ut => ut.Training.DueDate)
                    .Take(5).ToListAsync())
                    .Select(ut => new HomeActivityItem
                    {
                        Id = ut.Training.Id,
                        RecordId = ut.Training.TrainingId,
                        Title = ut.Training.Name,
                        Status = "Scheduled",
                        BadgeClass = "primary",
                        Date = ut.Training.DueDate ?? ut.Training.CreatedOn
                    }).ToList();
            }

            if (_context.Change != null)
            {
                vm.MyChanges = await _context.Change
                    .CountAsync(c => !c.IsDeleted
                                   && c.ProposedBy == currentUserName
                                   && c.Status != Change.ChangeStatus.Approved);

                vm.MyRecentChanges = (await _context.Change
                    .Where(c => !c.IsDeleted
                             && c.ProposedBy == currentUserName
                             && c.Status != Change.ChangeStatus.Approved)
                    .OrderByDescending(c => c.ProposedOn)
                    .Take(5).ToListAsync())
                    .Select(c => new HomeActivityItem
                    {
                        Id = c.Id, RecordId = c.ChangeId, Title = c.Title,
                        Status = c.Status switch
                        {
                            Change.ChangeStatus.PendingAssessment => "Pending Assessment",
                            Change.ChangeStatus.PendingApproval   => "Pending Approval",
                            _ => EnumHelper.Display(c.Status)
                        },
                        BadgeClass = c.Status switch
                        {
                            Change.ChangeStatus.Proposal          => "primary",
                            Change.ChangeStatus.Assessment        => "warning",
                            Change.ChangeStatus.PendingAssessment => "warning",
                            Change.ChangeStatus.Accepted          => "info",
                            Change.ChangeStatus.Implementation    => "info",
                            Change.ChangeStatus.PendingApproval   => "danger",
                            _ => "secondary"
                        },
                        Date = c.ProposedOn
                    }).ToList();
            }

            vm.MyRisks = await _context.Risk
                .CountAsync(r => !r.IsDeleted
                               && r.IdentifiedBy == currentUserName
                               && r.Status != Risk.RiskStatus.Closed);

            vm.MyRecentRisks = (await _context.Risk
                .Where(r => !r.IsDeleted
                         && r.IdentifiedBy == currentUserName
                         && r.Status != Risk.RiskStatus.Closed)
                .OrderByDescending(r => r.IdentifiedOn)
                .Take(5).ToListAsync())
                .Select(r => new HomeActivityItem
                {
                    Id = r.Id, RecordId = r.RiskId, Title = r.Title,
                    Status = r.Status switch
                    {
                        Risk.RiskStatus.PendingAssessment => "Pending Assessment",
                        Risk.RiskStatus.PendingReview     => "Pending Review",
                        _                                  => EnumHelper.Display(r.Status)
                    },
                    BadgeClass = r.Status switch
                    {
                        Risk.RiskStatus.Identification    => "secondary",
                        Risk.RiskStatus.Assessment        => "warning",
                        Risk.RiskStatus.PendingAssessment => "warning",
                        Risk.RiskStatus.Mitigation        => "info",
                        Risk.RiskStatus.PendingReview     => "warning",
                        Risk.RiskStatus.Review            => "warning",
                        _                                  => "secondary"
                    },
                    Date = r.IdentifiedOn
                }).ToList();

            // ── Pending My Action ─────────────────────────────────────────

            var isQM = User.IsInRole(Constants.QualityManagerRole) || User.IsInRole(Constants.AdministratorsRole);
            var isRM = User.IsInRole(Constants.RegulatoryManagerRole) || User.IsInRole(Constants.AdministratorsRole);

            vm.IsQualityManager    = isQM;
            vm.IsRegulatoryManager = isRM;

            if (isQM)
            {
                if (_context.Capa != null)
                    vm.PendingCAPAs = (await _context.Capa
                        .Where(c => !c.IsDeleted
                                 && (c.Status == Capa.CapaStatus.PendingAssessment
                                  || c.Status == Capa.CapaStatus.PendingApproval))
                        .OrderBy(c => c.DeterminedOn)
                        .Take(5).ToListAsync())
                        .Select(c => new HomeActivityItem
                        {
                            Id = c.Id, RecordId = c.CapaId, Title = c.Title,
                            Status = c.Status switch
                            {
                                Capa.CapaStatus.PendingAssessment => "Needs Acceptance",
                                Capa.CapaStatus.PendingApproval   => "Needs Approval",
                                _ => EnumHelper.Display(c.Status)
                            },
                            BadgeClass = c.Status == Capa.CapaStatus.PendingApproval ? "danger" : "warning",
                            Date = c.DeterminedOn
                        }).ToList();

                if (_context.Deviation != null)
                    vm.PendingDeviations = (await _context.Deviation
                        .Where(d => !d.IsDeleted
                                 && (d.Status == Deviation.DeviationStatus.PendingAcceptance
                                  || d.Status == Deviation.DeviationStatus.Resolution))
                        .OrderBy(d => d.IdentifiedOn)
                        .Take(5).ToListAsync())
                        .Select(d => new HomeActivityItem
                        {
                            Id = d.Id, RecordId = d.DeviationId, Title = d.Title,
                            Status = d.Status switch
                            {
                                Deviation.DeviationStatus.PendingAcceptance => "Needs Acceptance",
                                Deviation.DeviationStatus.Resolution        => "Needs Approval",
                                _ => EnumHelper.Display(d.Status)
                            },
                            BadgeClass = d.Status == Deviation.DeviationStatus.Resolution ? "danger" : "warning",
                            Date = d.IdentifiedOn
                        }).ToList();

                if (_context.Change != null)
                    vm.PendingChanges = (await _context.Change
                        .Where(c => !c.IsDeleted
                                 && (c.Status == Change.ChangeStatus.PendingAssessment
                                  || c.Status == Change.ChangeStatus.PendingApproval))
                        .OrderBy(c => c.ProposedOn)
                        .Take(5).ToListAsync())
                        .Select(c => new HomeActivityItem
                        {
                            Id = c.Id, RecordId = c.ChangeId, Title = c.Title,
                            Status = c.Status switch
                            {
                                Change.ChangeStatus.PendingAssessment => "Needs Acceptance",
                                Change.ChangeStatus.PendingApproval   => "Needs Approval",
                                _ => EnumHelper.Display(c.Status)
                            },
                            BadgeClass = c.Status == Change.ChangeStatus.PendingApproval ? "danger" : "warning",
                            Date = c.ProposedOn
                        }).ToList();

                vm.PendingAudits = (await _context.Audit
                    .Where(a => !a.IsDeleted
                             && (a.Status == Audit.AuditStatus.UnderReview
                              || a.Status == Audit.AuditStatus.Approved))
                    .OrderBy(a => a.ScheduledOn)
                    .Take(5).ToListAsync())
                    .Select(a => new HomeActivityItem
                    {
                        Id = a.Id, RecordId = a.AuditId, Title = a.Title,
                        Status = a.Status == Audit.AuditStatus.Approved ? "Needs Closure" : "Needs Approval",
                        BadgeClass = a.Status == Audit.AuditStatus.Approved ? "warning" : "danger",
                        Date = a.ScheduledOn
                    }).ToList();

                vm.PendingDocuments = (await _context.AppDocument
                    .Where(d => !d.IsDeleted && d.Status == AppDocument.DocumentStatus.UnderReview)
                    .OrderBy(d => d.AuthoredOn)
                    .Take(5).ToListAsync())
                    .Select(d => new HomeActivityItem
                    {
                        Id = d.Id, RecordId = d.DocumentId, Title = d.Title,
                        Status = "Needs Approval", BadgeClass = "danger",
                        Date = d.AuthoredOn
                    }).ToList();

                vm.PendingComplaints = (await _context.Complaint
                    .Where(c => !c.IsDeleted
                             && (c.Status == Complaint.ComplaintStatus.PendingAcceptance
                              || c.Status == Complaint.ComplaintStatus.Resolution))
                    .OrderBy(c => c.ReceivedOn)
                    .Take(5).ToListAsync())
                    .Select(c => new HomeActivityItem
                    {
                        Id = c.Id, RecordId = c.ComplaintId, Title = c.Title,
                        Status = c.Status switch
                        {
                            Complaint.ComplaintStatus.PendingAcceptance => "Needs Acceptance",
                            Complaint.ComplaintStatus.Resolution        => "Needs Closure",
                            _ => EnumHelper.Display(c.Status)
                        },
                        BadgeClass = c.Status == Complaint.ComplaintStatus.Resolution ? "danger" : "warning",
                        Date = c.ReceivedOn
                    }).ToList();

                vm.PendingRisks = (await _context.Risk
                    .Where(r => !r.IsDeleted
                             && (r.Status == Risk.RiskStatus.PendingAssessment
                              || r.Status == Risk.RiskStatus.PendingReview))
                    .OrderBy(r => r.IdentifiedOn)
                    .Take(5).ToListAsync())
                    .Select(r => new HomeActivityItem
                    {
                        Id = r.Id, RecordId = r.RiskId, Title = r.Title,
                        Status = r.Status switch
                        {
                            Risk.RiskStatus.PendingAssessment => "Needs Acceptance",
                            Risk.RiskStatus.PendingReview     => "Needs Closure",
                            _                                  => EnumHelper.Display(r.Status)
                        },
                        BadgeClass = r.Status == Risk.RiskStatus.PendingReview ? "danger" : "warning",
                        Date = r.IdentifiedOn
                    }).ToList();
            }

            if (isRM)
            {
                vm.PendingAdverseEvents = (await _context.AdverseEvent
                    .Where(ae => ae.Status == AdverseEvent.AEStatus.PendingApproval)
                    .OrderBy(ae => ae.TGADeadlineDate ?? DateTime.MaxValue)
                    .Take(5).ToListAsync())
                    .Select(ae => new HomeActivityItem
                    {
                        Id = ae.Id, RecordId = ae.AENumber,
                        Title = ae.MedDRAPT ?? "(No preferred term)",
                        Status = "Needs Approval", BadgeClass = "danger",
                        Date = ae.TGADeadlineDate ?? ae.ReportDate
                    }).ToList();

                vm.PendingSignalReviews = (await _context.PVSignalReview
                    .Where(s => s.Status == PVSignalReview.SignalStatus.Confirmed)
                    .OrderBy(s => s.DetectedOn)
                    .Take(5).ToListAsync())
                    .Select(s => new HomeActivityItem
                    {
                        Id = s.Id, RecordId = $"SIG-{s.Id:D4}", Title = s.MedDRAPT,
                        Status = "Confirmed — Action Required", BadgeClass = "danger",
                        Date = s.DetectedOn
                    }).ToList();

                vm.PendingLCRenewals = (await _context.LicenceCertificationRecords
                    .Where(lc => !lc.IsDeleted && lc.Status == LCStatus.RenewalDue)
                    .OrderBy(lc => lc.ExpiryDate ?? DateTime.MaxValue)
                    .Take(5).ToListAsync())
                    .Select(lc => new HomeActivityItem
                    {
                        Id = lc.Id, RecordId = lc.LCNumber, Title = lc.HolderName,
                        Status = "Renewal Due", BadgeClass = "danger",
                        Date = lc.ExpiryDate ?? lc.NextRenewalDue ?? today
                    }).ToList();
            }

            // ── Quality Events KPIs ───────────────────────────────────────

            vm.OpenCAPAs = _context.Capa != null
                ? await _context.Capa.CountAsync(c => !c.IsDeleted && c.Status != Capa.CapaStatus.Approved)
                : 0;

            vm.OpenDeviations = _context.Deviation != null
                ? await _context.Deviation.CountAsync(d => !d.IsDeleted && d.Status != Deviation.DeviationStatus.Approved)
                : 0;

            vm.OpenComplaints = await _context.Complaint
                .CountAsync(c => !c.IsDeleted && c.Status != Complaint.ComplaintStatus.Closed);

            vm.OpenChanges = _context.Change != null
                ? await _context.Change.CountAsync(c => !c.IsDeleted && c.Status != Change.ChangeStatus.Approved)
                : 0;

            // ── Quality Systems KPIs ──────────────────────────────────────

            vm.ActiveAudits = await _context.Audit
                .CountAsync(a => !a.IsDeleted
                              && a.Status != Audit.AuditStatus.Approved
                              && a.Status != Audit.AuditStatus.Closed);

            vm.OpenRisks = await _context.Risk
                .CountAsync(r => !r.IsDeleted && r.Status != Risk.RiskStatus.Closed);

            vm.ScheduledTrainings = await _context.Training
                .CountAsync(t => !t.IsDeleted && t.Status == Training.TrainingStatus.Scheduled);

            vm.DocumentsForReview = await _context.AppDocument
                .CountAsync(d => !d.IsDeleted
                              && (d.Status == AppDocument.DocumentStatus.Draft
                               || d.Status == AppDocument.DocumentStatus.UnderReview));

            vm.DocsReviewOverdue = await _context.AppDocument
                .CountAsync(d => !d.IsDeleted && !d.IsLocked
                              && d.Status == AppDocument.DocumentStatus.Approved
                              && d.ReviewDueDate.HasValue
                              && d.ReviewDueDate.Value.Date < today);

            vm.DocsReviewDueSoon = await _context.AppDocument
                .CountAsync(d => !d.IsDeleted && !d.IsLocked
                              && d.Status == AppDocument.DocumentStatus.Approved
                              && d.ReviewDueDate.HasValue
                              && d.ReviewDueDate.Value.Date >= today
                              && d.ReviewDueDate.Value.Date <= today.AddDays(30));

            // ── Supporting Documents KPIs ─────────────────────────────────
            var soonDate = today.AddDays(30);

            vm.SupportingDocsExpired =
                await _context.ProductDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < today)
              + await _context.MaterialDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < today)
              + await _context.ProcessDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < today)
              + await _context.AssetDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < today)
              + await _context.ServiceDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < today)
              + await _context.CustomerDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < today)
              + await _context.SupplierDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < today);

            vm.SupportingDocsExpiringSoon =
                await _context.ProductDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date >= today && d.ExpiryDate.Value.Date <= soonDate)
              + await _context.MaterialDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date >= today && d.ExpiryDate.Value.Date <= soonDate)
              + await _context.ProcessDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date >= today && d.ExpiryDate.Value.Date <= soonDate)
              + await _context.AssetDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date >= today && d.ExpiryDate.Value.Date <= soonDate)
              + await _context.ServiceDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date >= today && d.ExpiryDate.Value.Date <= soonDate)
              + await _context.CustomerDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date >= today && d.ExpiryDate.Value.Date <= soonDate)
              + await _context.SupplierDocument.CountAsync(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date >= today && d.ExpiryDate.Value.Date <= soonDate);

            // ── Regulatory KPIs ───────────────────────────────────────────

            vm.OpenAdverseEvents = await _context.AdverseEvent
                .CountAsync(ae => ae.Status != AdverseEvent.AEStatus.Closed
                               && ae.Status != AdverseEvent.AEStatus.Voided
                               && ae.Status != AdverseEvent.AEStatus.Approved);

            vm.LCRenewalsDue = await _context.LicenceCertificationRecords
                .CountAsync(lc => !lc.IsDeleted &&
                            (lc.Status == LCStatus.RenewalDue ||
                             (lc.ExpiryDate.HasValue && lc.ExpiryDate <= cutoff90 &&
                              lc.Status != LCStatus.Expired &&
                              lc.Status != LCStatus.Suspended &&
                              lc.Status != LCStatus.Withdrawn &&
                              lc.Status != LCStatus.NotApplicable)));

            vm.ActiveRIApplications = await _context.RegulatoryApplications
                .CountAsync(r => !r.IsDeleted &&
                            r.Status != RIApplicationStatus.Approved &&
                            r.Status != RIApplicationStatus.Rejected &&
                            r.Status != RIApplicationStatus.Withdrawn &&
                            r.Status != RIApplicationStatus.Suspended);

            vm.OpenSignalReviews = await _context.PVSignalReview
                .CountAsync(s => s.Status == PVSignalReview.SignalStatus.Open
                              || s.Status == PVSignalReview.SignalStatus.UnderReview
                              || s.Status == PVSignalReview.SignalStatus.Confirmed);

            // ── Global Active Items ───────────────────────────────────────

            if (_context.Capa != null)
            {
                vm.RecentCAPAs = (await _context.Capa
                    .Where(c => !c.IsDeleted && c.Status != Capa.CapaStatus.Approved)
                    .OrderByDescending(c => c.DeterminedOn)
                    .Take(5).ToListAsync())
                    .Select(c => new HomeActivityItem
                    {
                        Id = c.Id, RecordId = c.CapaId, Title = c.Title,
                        Status = c.Status switch
                        {
                            Capa.CapaStatus.PendingAssessment => "Pending Assessment",
                            Capa.CapaStatus.PendingApproval   => "Pending Approval",
                            _ => EnumHelper.Display(c.Status)
                        },
                        BadgeClass = c.Status switch
                        {
                            Capa.CapaStatus.Determination  => "primary",
                            Capa.CapaStatus.Assessment     => "warning",
                            Capa.CapaStatus.Accepted       => "info",
                            Capa.CapaStatus.Implementation => "info",
                            _ => "secondary"
                        },
                        Date = c.DeterminedOn
                    }).ToList();
            }

            if (_context.Deviation != null)
            {
                vm.RecentDeviations = (await _context.Deviation
                    .Where(d => !d.IsDeleted && d.Status != Deviation.DeviationStatus.Approved)
                    .OrderByDescending(d => d.IdentifiedOn)
                    .Take(5).ToListAsync())
                    .Select(d => new HomeActivityItem
                    {
                        Id = d.Id, RecordId = d.DeviationId, Title = d.Title,
                        Status = d.Status switch
                        {
                            Deviation.DeviationStatus.PendingAcceptance => "Pending Acceptance",
                            _ => EnumHelper.Display(d.Status)
                        },
                        BadgeClass = d.Status switch
                        {
                            Deviation.DeviationStatus.Identification    => "primary",
                            Deviation.DeviationStatus.Evaluation        => "info",
                            Deviation.DeviationStatus.PendingAcceptance => "warning",
                            Deviation.DeviationStatus.Accepted          => "info",
                            Deviation.DeviationStatus.Resolution        => "warning",
                            _ => "secondary"
                        },
                        Date = d.IdentifiedOn
                    }).ToList();
            }

            vm.RecentComplaints = (await _context.Complaint
                .Where(c => !c.IsDeleted && c.Status != Complaint.ComplaintStatus.Closed)
                .OrderByDescending(c => c.ReceivedOn)
                .Take(5).ToListAsync())
                .Select(c => new HomeActivityItem
                {
                    Id = c.Id, RecordId = c.ComplaintId, Title = c.Title,
                    Status = c.Status switch
                    {
                        Complaint.ComplaintStatus.PendingAcceptance => "Pending Acceptance",
                        _ => EnumHelper.Display(c.Status)
                    },
                    BadgeClass = c.Status switch
                    {
                        Complaint.ComplaintStatus.Received          => "primary",
                        Complaint.ComplaintStatus.Evaluation        => "info",
                        Complaint.ComplaintStatus.PendingAcceptance => "warning",
                        Complaint.ComplaintStatus.Resolution        => "warning",
                        _ => "secondary"
                    },
                    Date = c.ReceivedOn
                }).ToList();

            vm.RecentRisks = (await _context.Risk
                .Where(r => !r.IsDeleted && r.Status != Risk.RiskStatus.Closed)
                .OrderByDescending(r => r.IdentifiedOn)
                .Take(5).ToListAsync())
                .Select(r => new HomeActivityItem
                {
                    Id = r.Id, RecordId = r.RiskId, Title = r.Title,
                    Status = r.Status switch
                    {
                        Risk.RiskStatus.PendingAssessment => "Pending Assessment",
                        Risk.RiskStatus.PendingReview     => "Pending Review",
                        _                                  => EnumHelper.Display(r.Status)
                    },
                    BadgeClass = r.Status switch
                    {
                        Risk.RiskStatus.Identification    => "secondary",
                        Risk.RiskStatus.Assessment        => "warning",
                        Risk.RiskStatus.PendingAssessment => "warning",
                        Risk.RiskStatus.Mitigation        => "info",
                        Risk.RiskStatus.PendingReview     => "warning",
                        Risk.RiskStatus.Review            => "warning",
                        _                                  => "secondary"
                    },
                    Date = r.IdentifiedOn
                }).ToList();

            vm.RecentAudits = (await _context.Audit
                .Where(a => !a.IsDeleted
                         && a.Status != Audit.AuditStatus.Approved
                         && a.Status != Audit.AuditStatus.Closed)
                .OrderByDescending(a => a.ScheduledOn)
                .Take(5).ToListAsync())
                .Select(a => new HomeActivityItem
                {
                    Id = a.Id, RecordId = a.AuditId, Title = a.Title,
                    Status = a.Status switch
                    {
                        Audit.AuditStatus.InProgress  => "In Progress",
                        Audit.AuditStatus.ReportDraft => "Report Draft",
                        Audit.AuditStatus.UnderReview => "Under Review",
                        _ => EnumHelper.Display(a.Status)
                    },
                    BadgeClass = a.Status switch
                    {
                        Audit.AuditStatus.Scheduled   => "primary",
                        Audit.AuditStatus.InProgress  => "warning",
                        Audit.AuditStatus.ReportDraft => "warning",
                        Audit.AuditStatus.UnderReview => "info",
                        _ => "secondary"
                    },
                    Date = a.ScheduledOn
                }).ToList();

            vm.RecentDocuments = (await _context.AppDocument
                .Where(d => !d.IsDeleted
                         && (d.Status == AppDocument.DocumentStatus.Draft
                          || d.Status == AppDocument.DocumentStatus.UnderReview))
                .OrderByDescending(d => d.AuthoredOn)
                .Take(5).ToListAsync())
                .Select(d => new HomeActivityItem
                {
                    Id = d.Id, RecordId = d.DocumentId, Title = d.Title,
                    Status = EnumHelper.Display(d.Status),
                    BadgeClass = d.Status switch
                    {
                        AppDocument.DocumentStatus.Draft       => "primary",
                        AppDocument.DocumentStatus.UnderReview => "warning",
                        _ => "secondary"
                    },
                    Date = d.AuthoredOn
                }).ToList();

            vm.RecentTrainings = (await _context.Training
                .Where(t => !t.IsDeleted && t.Status == Training.TrainingStatus.Scheduled)
                .OrderBy(t => t.DueDate)
                .Take(5).ToListAsync())
                .Select(t => new HomeActivityItem
                {
                    Id = t.Id, RecordId = t.TrainingId, Title = t.Name,
                    Status = "Scheduled", BadgeClass = "primary",
                    Date = t.DueDate ?? t.CreatedOn
                }).ToList();

            // ── Regulatory Active Items ───────────────────────────────────

            vm.RecentAdverseEvents = (await _context.AdverseEvent
                .Where(ae => ae.Status != AdverseEvent.AEStatus.Closed
                          && ae.Status != AdverseEvent.AEStatus.Voided
                          && ae.Status != AdverseEvent.AEStatus.Approved)
                .OrderBy(ae => ae.TGADeadlineDate ?? DateTime.MaxValue)
                .Take(5).ToListAsync())
                .Select(ae => new HomeActivityItem
                {
                    Id = ae.Id, RecordId = ae.AENumber,
                    Title = ae.MedDRAPT ?? "(No preferred term)",
                    Status = ae.Status switch
                    {
                        AdverseEvent.AEStatus.UnderMedicalReview => "Under Medical Review",
                        AdverseEvent.AEStatus.PendingApproval    => "Pending Approval",
                        _ => EnumHelper.Display(ae.Status)
                    },
                    BadgeClass = ae.Status switch
                    {
                        AdverseEvent.AEStatus.Draft              => "primary",
                        AdverseEvent.AEStatus.UnderMedicalReview => "warning",
                        AdverseEvent.AEStatus.PendingApproval    => "info",
                        _ => "secondary"
                    },
                    Date = ae.TGADeadlineDate ?? ae.ReportDate
                }).ToList();

            vm.LCExpiringSoon = (await _context.LicenceCertificationRecords
                .Where(lc => !lc.IsDeleted &&
                            (lc.Status == LCStatus.RenewalDue ||
                             (lc.ExpiryDate.HasValue && lc.ExpiryDate <= cutoff90 &&
                              lc.Status != LCStatus.Expired &&
                              lc.Status != LCStatus.Suspended &&
                              lc.Status != LCStatus.Withdrawn &&
                              lc.Status != LCStatus.NotApplicable)))
                .OrderBy(lc => lc.ExpiryDate ?? DateTime.MaxValue)
                .Take(5).ToListAsync())
                .Select(lc => new HomeActivityItem
                {
                    Id = lc.Id, RecordId = lc.LCNumber,
                    Title = lc.HolderName,
                    Status = lc.Status switch
                    {
                        LCStatus.RenewalDue        => "Renewal Due",
                        LCStatus.ConditionsApplied => "Active",
                        LCStatus.IssuedActive      => "Active",
                        _ => EnumHelper.Display(lc.Status)
                    },
                    BadgeClass = lc.Status switch
                    {
                        LCStatus.RenewalDue        => "danger",
                        LCStatus.ConditionsApplied => "success",
                        _ => "warning"
                    },
                    Date = lc.ExpiryDate ?? lc.NextRenewalDue ?? today
                }).ToList();

            vm.RecentRIApplications = (await _context.RegulatoryApplications
                .Where(r => !r.IsDeleted &&
                            r.Status != RIApplicationStatus.Approved &&
                            r.Status != RIApplicationStatus.Rejected &&
                            r.Status != RIApplicationStatus.Withdrawn &&
                            r.Status != RIApplicationStatus.Suspended)
                .OrderBy(r => r.TargetDecisionDate ?? DateTime.MaxValue)
                .Take(5).ToListAsync())
                .Select(r => new HomeActivityItem
                {
                    Id = r.Id, RecordId = r.RINumber,
                    Title = r.ProductName,
                    Status = r.Status switch
                    {
                        RIApplicationStatus.UnderReview       => "Under Review",
                        RIApplicationStatus.ClockStop         => "Clock Stop",
                        RIApplicationStatus.ConditionsActive  => "Conditions Active",
                        RIApplicationStatus.VariationRequired => "Variation Required",
                        RIApplicationStatus.RenewalDue        => "Renewal Due",
                        _ => EnumHelper.Display(r.Status)
                    },
                    BadgeClass = r.Status switch
                    {
                        RIApplicationStatus.Strategy          => "secondary",
                        RIApplicationStatus.Preparation       => "primary",
                        RIApplicationStatus.Submitted         => "info",
                        RIApplicationStatus.UnderReview       => "info",
                        RIApplicationStatus.ClockStop         => "warning",
                        RIApplicationStatus.ConditionsActive  => "warning",
                        RIApplicationStatus.VariationRequired => "warning",
                        RIApplicationStatus.RenewalDue        => "danger",
                        _ => "secondary"
                    },
                    Date = r.TargetDecisionDate ?? r.SubmissionDate ?? r.CreatedAt
                }).ToList();

            vm.RecentSignalReviews = (await _context.PVSignalReview
                .Where(s => s.Status == PVSignalReview.SignalStatus.Open
                         || s.Status == PVSignalReview.SignalStatus.UnderReview
                         || s.Status == PVSignalReview.SignalStatus.Confirmed)
                .OrderByDescending(s => s.DetectedOn)
                .Take(5).ToListAsync())
                .Select(s => new HomeActivityItem
                {
                    Id = s.Id,
                    RecordId = $"SIG-{s.Id:D4}",
                    Title = s.MedDRAPT,
                    Status = s.Status switch
                    {
                        PVSignalReview.SignalStatus.UnderReview => "Under Review",
                        _ => EnumHelper.Display(s.Status)
                    },
                    BadgeClass = s.Status switch
                    {
                        PVSignalReview.SignalStatus.Open        => "primary",
                        PVSignalReview.SignalStatus.UnderReview => "warning",
                        PVSignalReview.SignalStatus.Confirmed   => "danger",
                        _ => "secondary"
                    },
                    Date = s.DetectedOn
                }).ToList();

            return View(vm);
        }

        // Public (linked from the login page footer): licence, disclaimer and source-code offer (AGPLv3 s.13)
        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        public async Task<ActionResult> Dashboard()
        {
            IQueryable<AuthoredOnGroup> data =
                from document in _context.AppDocument
                group document by document.AuthoredOn.Date into dateGroup
                select new AuthoredOnGroup()
                {
                    AuthoredOn = dateGroup.Key,
                    DocumentCount = dateGroup.Count()
                };

            return View(await data.AsNoTracking().ToListAsync());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
