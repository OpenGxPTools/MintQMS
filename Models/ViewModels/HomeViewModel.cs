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

namespace OpenQMS.Models.ViewModels
{
    public class HomeActivityItem
    {
        public int Id { get; set; }
        public string RecordId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "secondary";
        public DateTime Date { get; set; }
    }

    public class HomeViewModel
    {
        // ── My Items (personalised to the logged-in user) ─────────────────
        public int MyCAPAs { get; set; }
        public int MyDeviations { get; set; }
        public int MyDocuments { get; set; }
        public int MyTrainings { get; set; }
        public int MyChanges { get; set; }
        public int MyRisks { get; set; }
        public List<HomeActivityItem> MyRecentCAPAs { get; set; } = new();
        public List<HomeActivityItem> MyRecentDeviations { get; set; } = new();
        public List<HomeActivityItem> MyRecentDocuments { get; set; } = new();
        public List<HomeActivityItem> MyRecentTrainings { get; set; } = new();
        public List<HomeActivityItem> MyRecentChanges { get; set; } = new();
        public List<HomeActivityItem> MyRecentRisks { get; set; } = new();

        // ── Pending My Action (role-gated) ────────────────────────────────
        public bool IsQualityManager { get; set; }
        public bool IsRegulatoryManager { get; set; }

        // Quality Manager pending
        public List<HomeActivityItem> PendingCAPAs { get; set; } = new();
        public List<HomeActivityItem> PendingDeviations { get; set; } = new();
        public List<HomeActivityItem> PendingChanges { get; set; } = new();
        public List<HomeActivityItem> PendingAudits { get; set; } = new();
        public List<HomeActivityItem> PendingDocuments { get; set; } = new();
        public List<HomeActivityItem> PendingComplaints { get; set; } = new();
        public List<HomeActivityItem> PendingRisks { get; set; } = new();

        // Regulatory Manager pending
        public List<HomeActivityItem> PendingAdverseEvents { get; set; } = new();
        public List<HomeActivityItem> PendingSignalReviews { get; set; } = new();
        public List<HomeActivityItem> PendingLCRenewals { get; set; } = new();

        // ── Quality Events KPI counts ─────────────────────────────────────
        public int OpenCAPAs { get; set; }
        public int OpenDeviations { get; set; }
        public int OpenComplaints { get; set; }
        public int OpenChanges { get; set; }

        // ── Quality Systems KPI counts ────────────────────────────────────
        public int ActiveAudits { get; set; }
        public int OpenRisks { get; set; }
        public int ScheduledTrainings { get; set; }
        public int DocumentsForReview { get; set; }
        public int DocsReviewOverdue { get; set; }
        public int DocsReviewDueSoon { get; set; }

        // ── Supporting Documents KPI counts ──────────────────────────
        public int SupportingDocsExpired { get; set; }
        public int SupportingDocsExpiringSoon { get; set; }

        // ── Regulatory KPI counts ─────────────────────────────────────────
        public int OpenAdverseEvents { get; set; }
        public int LCRenewalsDue { get; set; }
        public int ActiveRIApplications { get; set; }
        public int OpenSignalReviews { get; set; }

        // ── Global active items (top 5 per module) ────────────────────────
        public List<HomeActivityItem> RecentCAPAs { get; set; } = new();
        public List<HomeActivityItem> RecentRisks { get; set; } = new();
        public List<HomeActivityItem> RecentDeviations { get; set; } = new();
        public List<HomeActivityItem> RecentComplaints { get; set; } = new();
        public List<HomeActivityItem> RecentAudits { get; set; } = new();
        public List<HomeActivityItem> RecentDocuments { get; set; } = new();
        public List<HomeActivityItem> RecentTrainings { get; set; } = new();

        // ── Regulatory active items ───────────────────────────────────────
        public List<HomeActivityItem> RecentAdverseEvents { get; set; } = new();
        public List<HomeActivityItem> LCExpiringSoon { get; set; } = new();
        public List<HomeActivityItem> RecentRIApplications { get; set; } = new();
        public List<HomeActivityItem> RecentSignalReviews { get; set; } = new();
    }
}
