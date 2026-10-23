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
    public class Deviation
    {
        public int Id { get; set; }
        [Display(Name = "ID")]
        public string DeviationId { get; set; } = string.Empty;
        public int? ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
        public int? ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
        public int? AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }
        public int? MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }
        public int? SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
        [Display(Name = "Linked CAPA ID")]
        public int? LinkedCapaId { get; set; }
        public virtual ICollection<Capa>? Capas { get; set; }
        public string Title { get; set; } = string.Empty;
        [Display(Name = "Event Type")]
        public DeviationEventType? EventType { get; set; }
        [Display(Name = "Severity")]
        public DeviationSeverity? Severity { get; set; }
        public string Identification { get; set; } = string.Empty;
        [Display(Name = "Identified By")]
        public string IdentifiedBy { get; set; } = string.Empty;
        [Display(Name = "Identified On")]
        public DateTime IdentifiedOn { get; set; }
        [Display(Name = "Target Resolution Date")]
        public DateTime? TargetResolutionDate { get; set; }
        public string? Evaluation { get; set; }
        [Display(Name = "Evaluated By")]
        public string? EvaluatedBy { get; set; }
        [Display(Name = "Evaluated On")]
        public DateTime? EvaluatedOn { get; set; }
        public string? Investigation { get; set; }
        [Display(Name = "Investigated By")]
        public string? InvestigatedBy { get; set; }
        [Display(Name = "Investigated On")]
        public DateTime? InvestigatedOn { get; set; }
        [Display(Name = "Root Cause")]
        public string? RootCause { get; set; }
        [Display(Name = "Root Cause By")]
        public string? RootCauseBy { get; set; }
        [Display(Name = "Root Cause On")]
        public DateTime? RootCauseOn { get; set; }
        [Display(Name = "Root Cause Category")]
        public RootCauseCategory? RootCauseCategory { get; set; }
        [Display(Name = "Corrective and Preventive Action")]
        public string? CorrectiveAction { get; set; }
        [Display(Name = "Action By")]
        public string? ActionBy { get; set; }
        [Display(Name = "Action On")]
        public DateTime? ActionOn { get; set; }
        [Display(Name = "Accepted By")]
        public string? AcceptedBy { get; set; }
        [Display(Name = "Accepted On")]
        public DateTime? AcceptedOn { get; set; }
        public string? Resolution { get; set; }
        [Display(Name = "Resolved By")]
        public string? ResolvedBy { get; set; }
        [Display(Name = "Resolved On")]
        public DateTime? ResolvedOn { get; set; }
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }
        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }
        public string? ExportFilePath { get; set; }
        public DeviationStatus Status { get; set; }
        public enum DeviationStatus
        {
            Identification    = 0,
            Evaluation        = 1,
            [Display(Name = "Pending Acceptance")]
            PendingAcceptance = 5,   // explicit value — preserves existing DB records (Accepted=2, Resolution=3, Approved=4)
            Accepted          = 2,
            Resolution        = 3,
            Approved          = 4
        }

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
        public ICollection<DeviationProduct>?  LinkedProducts  { get; set; }
        public ICollection<DeviationProcess>?  LinkedProcesses { get; set; }
        public ICollection<DeviationAsset>?    LinkedAssets    { get; set; }
        public ICollection<DeviationMaterial>? LinkedMaterials { get; set; }
        public ICollection<DeviationSupplier>? LinkedSuppliers { get; set; }
        // Reverse navigation — CAPAs that reference this deviation via the CapaDeviation junction
        public ICollection<CapaDeviation>?     LinkedCapas     { get; set; }

        public ICollection<DeviationAttachment>? Attachments { get; set; }
        public ICollection<DeviationAuditLog>? AuditLogs { get; set; }
    }
}
