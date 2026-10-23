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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class Maintenance
    {
        public int Id { get; set; }

        [Display(Name = "Maintenance ID")]
        public string MaintenanceId { get; set; } = string.Empty;

        // Asset Reference (Required)
        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }

        // Optional References
        public int? ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }

        public int? DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }

        public int? CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }

        // Basic Information
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Maintenance Type")]
        public MaintenanceType Type { get; set; }

        [Display(Name = "Frequency")]
        public MaintenanceFrequency Frequency { get; set; }

        [Display(Name = "Priority")]
        public MaintenancePriority Priority { get; set; }

        // Scheduling
        [Display(Name = "Scheduled Date")]
        public DateTime ScheduledDate { get; set; }

        [Display(Name = "Scheduled By")]
        public string ScheduledBy { get; set; } = string.Empty;

        [Display(Name = "Scheduled On")]
        public DateTime ScheduledOn { get; set; }

        // Execution
        [Display(Name = "Work Performed")]
        public string? WorkPerformed { get; set; }

        [Display(Name = "Performed By")]
        public string? PerformedBy { get; set; }

        [Display(Name = "Performed On")]
        public DateTime? PerformedOn { get; set; }

        [Display(Name = "Actual Start Time")]
        public DateTime? ActualStartTime { get; set; }

        [Display(Name = "Actual End Time")]
        public DateTime? ActualEndTime { get; set; }

        [Display(Name = "Downtime (Hours)")]
        public decimal? DowntimeHours { get; set; }

        // Parts & Cost
        [Display(Name = "Parts Used")]
        public string? PartsUsed { get; set; }

        [Display(Name = "Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Cost { get; set; }

        // Review
        [Display(Name = "Review Comments")]
        public string? ReviewComments { get; set; }

        [Display(Name = "Reviewed By")]
        public string? ReviewedBy { get; set; }

        [Display(Name = "Reviewed On")]
        public DateTime? ReviewedOn { get; set; }

        // Approval
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }

        // Status & Export
        public MaintenanceStatus Status { get; set; }

        public string? ExportFilePath { get; set; }

        // Next Maintenance (for recurring)
        [Display(Name = "Next Scheduled Date")]
        public DateTime? NextScheduledDate { get; set; }

        // Recurring chain — links occurrences together
        public int? ParentMaintenanceId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("ParentMaintenanceId")]
        public virtual Maintenance? ParentMaintenance { get; set; }

        // Suspension
        [Display(Name = "Suspended By")]
        public string? SuspendedBy { get; set; }

        [Display(Name = "Suspended On")]
        public DateTime? SuspendedOn { get; set; }

        [Display(Name = "Suspension Reason")]
        public string? SuspensionReason { get; set; }

        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }

        // ── Gap 13: Retention & Disposal ─────────────────────────────────────
        public DateTime?          RetainUntil                   { get; set; }
        public bool               RetentionExtended             { get; set; } = false;
        public RetentionHoldReason? RetentionExtendedReason     { get; set; }
        public string?            RetentionExtendedComments     { get; set; }
        public string?            RetentionExtendedBy           { get; set; }
        public DateTime?          RetentionExtendedOn           { get; set; }
        public string?            RetentionExtendedRemovalReason { get; set; }
        public string?            RetentionExtendedRemovedBy    { get; set; }
        public DateTime?          RetentionExtendedRemovedOn    { get; set; }
        public DateTime?          RetentionHoldCooldownUntil    { get; set; }

        // Many-to-many junction collections
        public ICollection<MaintenanceDeviation>? LinkedDeviations { get; set; }
        public ICollection<MaintenanceCapa>?      LinkedCapas      { get; set; }

        // Attachments
        public ICollection<MaintenanceAttachment>? Attachments { get; set; }
        public ICollection<MaintenanceAuditLog>? AuditLogs { get; set; }

        // Enums
        public enum MaintenanceType
        {
            [Display(Name = "Preventive Maintenance")]
            Preventive,

            [Display(Name = "Corrective Maintenance")]
            Corrective,

            [Display(Name = "Calibration")]
            Calibration,

            [Display(Name = "Qualification (IQ/OQ/PQ)")]
            Qualification,

            [Display(Name = "Cleaning")]
            Cleaning,

            [Display(Name = "Inspection")]
            Inspection
        }

        public enum MaintenanceFrequency
        {
            [Display(Name = "One-time")]
            OneTime,

            [Display(Name = "Weekly")]
            Weekly,

            [Display(Name = "Monthly")]
            Monthly,

            [Display(Name = "Quarterly")]
            Quarterly,

            [Display(Name = "Semi-Annual")]
            SemiAnnual,

            [Display(Name = "Annual")]
            Annual,

            [Display(Name = "As Needed")]
            AsNeeded
        }

        public enum MaintenancePriority
        {
            [Display(Name = "Low")]
            Low,

            [Display(Name = "Medium")]
            Medium,

            [Display(Name = "High")]
            High,

            [Display(Name = "Critical")]
            Critical
        }

        public enum MaintenanceStatus
        {
            [Display(Name = "Scheduled")]
            Scheduled,

            [Display(Name = "In Progress")]
            InProgress,

            [Display(Name = "Completed")]
            Completed,

            [Display(Name = "Reviewed")]
            Reviewed,

            [Display(Name = "Approved")]
            Approved,

            [Display(Name = "Overdue")]
            Overdue,

            [Display(Name = "Suspended")]
            Suspended
        }
    }
}
