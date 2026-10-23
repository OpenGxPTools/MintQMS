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
    public class Complaint
    {
        public int Id { get; set; }
        
        [Display(Name = "Complaint ID")]
        public string ComplaintId { get; set; } = string.Empty;
        
        public int? ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
        
        public int? ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
        
        public int? MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }

        public int? SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
        
        public string Title { get; set; } = string.Empty;
        
        public int? CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [Display(Name = "Customer Legal Name")]
        public string? CustomerName { get; set; }
        
        [Display(Name = "Customer Contact")]
        public string? CustomerContact { get; set; }
        
        [Display(Name = "Customer Reference")]
        public string? CustomerReference { get; set; }
        
        public string Description { get; set; } = string.Empty;
        
        public string? Evaluation { get; set; }
        
        public ComplaintSeverity Severity { get; set; }
        
        [Display(Name = "Received By")]
        public string ReceivedBy { get; set; } = string.Empty;

        [Display(Name = "Received On")]
        public DateTime ReceivedOn { get; set; }

        [Display(Name = "Target Resolution Date")]
        public DateTime? TargetResolutionDate { get; set; }
        
        public string? Investigation { get; set; }

        [Display(Name = "Investigated By")]
        public string? InvestigatedBy { get; set; }

        [Display(Name = "Investigated On")]
        public DateTime? InvestigatedOn { get; set; }

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
        
        [Display(Name = "Closed By")]
        public string? ClosedBy { get; set; }
        
        [Display(Name = "Closed On")]
        public DateTime? ClosedOn { get; set; }
        
        public string? ExportFilePath { get; set; }
        
        [Display(Name = "Linked CAPA ID")]
        public int? LinkedCapaId { get; set; }

        [Display(Name = "Linked Adverse Event ID")]
        public int? LinkedAEId { get; set; }

        // Relationship configured in ApplicationDbContext.OnModelCreating (independent
        // one-way link; FK also exists on AdverseEvent.LinkedComplaintId, kept in sync by controllers).
        public virtual AdverseEvent? LinkedAdverseEvent { get; set; }

        public ComplaintStatus Status { get; set; }
        
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
        public ICollection<ComplaintProduct>?  LinkedProducts  { get; set; }
        public ICollection<ComplaintProcess>?  LinkedProcesses { get; set; }
        public ICollection<ComplaintMaterial>? LinkedMaterials { get; set; }
        public ICollection<ComplaintSupplier>? LinkedSuppliers { get; set; }
        // Reverse navigation — CAPAs that reference this complaint via the CapaComplaint junction
        public ICollection<CapaComplaint>?     LinkedCapas     { get; set; }

        public ICollection<ComplaintAttachment>? Attachments { get; set; }
        public ICollection<ComplaintAuditLog>? AuditLogs { get; set; }

        public enum ComplaintStatus
        {
            Received          = 0,
            Evaluation        = 1,
            [Display(Name = "Pending Acceptance")]
            PendingAcceptance = 2,
            Accepted          = 5,   // explicit value — preserves existing DB records (Resolution=3, Closed=4)
            Resolution        = 3,
            Closed            = 4
        }
        
        public enum ComplaintSeverity
        {
            Minor,
            Major,
            Critical
        }
    }
}
