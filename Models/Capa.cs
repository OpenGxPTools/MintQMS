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
    public class Capa
    {
        public int Id { get; set; }
        [Display(Name = "ID")]
        public string CapaId { get; set; } = string.Empty;
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
        public int? DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
        public int? ComplaintId { get; set; }
        [ForeignKey("ComplaintId")]
        public virtual Complaint? Complaint { get; set; }
        public int? AuditId { get; set; }
        [ForeignKey("AuditId")]
        public virtual Audit? Audit { get; set; }
        public int? LinkedAdverseEventId { get; set; }
        // Relationship configured in ApplicationDbContext.OnModelCreating (independent
        // one-way link; FK also exists on AdverseEvent.LinkedCapaId, kept in sync by controllers).
        public virtual AdverseEvent? LinkedAdverseEvent { get; set; }
        public virtual ICollection<Change>? Changes { get; set; }
        [Display(Name = "CAPA Type")]
        public CapaType Type { get; set; } = CapaType.StandardCapa;
        public string Title { get; set; }
        [Display(Name = "Corrective Action")]
        public string CorrectiveAction { get; set; }
        [Display(Name = "Preventive Action")]
        public string PreventiveAction { get; set; }
        [Display(Name = "Target Completion Date")]
        public DateTime? TargetCompletionDate { get; set; }
        [Display(Name = "Determined By")]
        public string DeterminedBy { get; set; } = string.Empty;
        [Display(Name = "Determined On")]
        public DateTime DeterminedOn { get; set; }
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
        public CapaStatus Status { get; set; }
        public enum CapaStatus
        {
            Determination     = 0,
            Assessment        = 1,
            [Display(Name = "Pending Assessment")]
            PendingAssessment = 5,  // explicit value — keeps existing DB rows intact
            Accepted          = 2,
            Implementation    = 3,
            [Display(Name = "Pending Approval")]
            PendingApproval   = 6,  // explicit value — keeps existing DB rows intact
            Approved          = 4
        }

        public enum CapaType
        {
            // Default — a corrective/preventive action arising from an identified problem.
            [Display(Name = "Standard CAPA")]
            StandardCapa = 0,

            // A planned business improvement initiative — not driven by a current problem and
            // not a mandatory action; pursued because the organisation elects to improve.
            [Display(Name = "Improvement Opportunity")]
            ImprovementOpportunity = 1,

            // A recall / market action — the master record for recovering, correcting or
            // containing distributed product (see the Product Recall & Field Correction SOP).
            [Display(Name = "Recall / Market Action")]
            Recall = 2
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
        public ICollection<CapaProduct>?   LinkedProducts   { get; set; }
        public ICollection<CapaProcess>?   LinkedProcesses  { get; set; }
        public ICollection<CapaAsset>?     LinkedAssets     { get; set; }
        public ICollection<CapaMaterial>?  LinkedMaterials  { get; set; }
        public ICollection<CapaSupplier>?  LinkedSuppliers  { get; set; }
        public ICollection<CapaDeviation>? LinkedDeviations { get; set; }
        public ICollection<CapaComplaint>? LinkedComplaints { get; set; }
        public ICollection<CapaAudit>?     LinkedAudits     { get; set; }

        public ICollection<CapaAttachment>? Attachments { get; set; }
        public ICollection<CapaAuditLog>? AuditLogs { get; set; }
    }
}
