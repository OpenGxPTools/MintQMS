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
    public class Change
    {
        public int Id { get; set; }
        [Display(Name = "ID")]
        public string ChangeId { get; set; } = string.Empty;
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
        public int? CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public string Title { get; set; }
        public string Proposal { get; set; }
        [Display(Name = "Target Implementation Date")]
        public DateTime? TargetImplementationDate { get; set; }

        // ── Assessment-stage classification (Change Control Management SOP §6.2, §6.4) ──
        [Display(Name = "Risk Classification")]
        public ChangeRiskClassification? RiskClassification { get; set; }
        [Display(Name = "Change Type")]
        public ChangeTypeCategory? ChangeType { get; set; }
        [Display(Name = "Permanent / Temporary / Emergency")]
        public ChangeNature? Nature { get; set; }
        [Display(Name = "Detailed Action Plan Required")]
        public bool DetailedActionPlanRequired { get; set; } = false;
        [Display(Name = "Proposed By")]
        public string ProposedBy { get; set; } = string.Empty;
        [Display(Name = "Proposed On")]
        public DateTime ProposedOn { get; set; }
        public string? Assessment { get; set; }
        [Display(Name = "Assessed By")]
        public string? AssessedBy { get; set; }
        [Display(Name = "Assessed On")]
        public DateTime? AssessedOn { get; set; }
        [Display(Name = "Accepted By")]
        public string? AcceptedBy { get; set; }
        [Display(Name = "Accepted On")]
        public DateTime? AcceptedOn { get; set; }
        public string? Implementation { get; set; }
        [Display(Name = "Implemented By")]
        public string? ImplementedBy { get; set; }
        [Display(Name = "Implemented On")]
        public DateTime? ImplementedOn { get; set; }
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }
        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }
        public string? ExportFilePath { get; set; }
        public ChangeStatus Status { get; set; }
        public enum ChangeStatus
        {
            Proposal          = 0,
            Assessment        = 1,
            [Display(Name = "Pending Assessment")]
            PendingAssessment = 5,  // explicit value — keeps existing DB rows intact
            Accepted          = 2,
            Implementation    = 3,
            [Display(Name = "Pending Approval")]
            PendingApproval   = 6,  // explicit value — keeps existing DB rows intact
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
        public ICollection<ChangeProduct>?  LinkedProducts  { get; set; }
        public ICollection<ChangeProcess>?  LinkedProcesses { get; set; }
        public ICollection<ChangeAsset>?    LinkedAssets    { get; set; }
        public ICollection<ChangeMaterial>? LinkedMaterials { get; set; }
        public ICollection<ChangeSupplier>? LinkedSuppliers { get; set; }
        public ICollection<ChangeCapa>?     LinkedCapas     { get; set; }

        public ICollection<ChangeActionPlanItem>? ActionPlanItems { get; set; }

        public ICollection<ChangeAttachment>? Attachments { get; set; }
        public ICollection<ChangeAuditLog>? AuditLogs { get; set; }
    }
}
