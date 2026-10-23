/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2025  C-realize IT Services SRL (https://www.c-realize.com)
This program is offered under a commercial and under the AGPL license.
*/
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class Audit
    {
        public int Id { get; set; }
        
        [Display(Name = "Audit ID")]
        public string AuditId { get; set; } = string.Empty;
        
        public string Title { get; set; } = string.Empty;
        
        [Display(Name = "Audit Type")]
        public AuditType Type { get; set; }
        
        [Display(Name = "Audit Scope")]
        public AuditScope Scope { get; set; }
        
        public string Description { get; set; } = string.Empty;
        
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

        // Only populated when Type == Supplier
        public int? SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
        
        [Display(Name = "Planned Start Date")]
        public DateTime? PlannedStartDate { get; set; }
        
        [Display(Name = "Planned End Date")]
        public DateTime? PlannedEndDate { get; set; }
        
        [Display(Name = "Actual Start Date")]
        public DateTime? ActualStartDate { get; set; }
        
        [Display(Name = "Actual End Date")]
        public DateTime? ActualEndDate { get; set; }
        
        [Display(Name = "Audit Location")]
        public string? AuditLocation { get; set; }
        
        [Display(Name = "Lead Auditor")]
        public string? LeadAuditor { get; set; }
        
        [Display(Name = "Audit Team")]
        public string? AuditTeam { get; set; }
        
        [Display(Name = "Auditee (Department/Supplier)")]
        public string? Auditee { get; set; }
        
        [Display(Name = "Auditee Contact")]
        public string? AuditeeContact { get; set; }
        
        [Display(Name = "Findings Summary")]
        public string? FindingsSummary { get; set; }
        
        [Display(Name = "Critical Findings")]
        public int CriticalFindings { get; set; }
        
        [Display(Name = "Major Findings")]
        public int MajorFindings { get; set; }
        
        [Display(Name = "Minor Findings")]
        public int MinorFindings { get; set; }
        
        [Display(Name = "Observations")]
        public int Observations { get; set; }
        
        [Display(Name = "Overall Rating")]
        public AuditRating? OverallRating { get; set; }
        
        [Display(Name = "CAPA Required")]
        public bool CAPARequired { get; set; }
        
        [Display(Name = "Corrective and Preventive Action")]
        public string? CorrectiveAction { get; set; }
        
        [Display(Name = "Linked CAPA ID")]
        public int? LinkedCapaId { get; set; }
        
        [Display(Name = "Follow-Up Date")]
        public DateTime? FollowUpDate { get; set; }
        
        [Display(Name = "Follow-Up Completed")]
        public bool FollowUpCompleted { get; set; }
        
        [Display(Name = "Audit Plan")]
        public string? AuditPlan { get; set; }
        
        [Display(Name = "Audit Report")]
        public string? AuditReport { get; set; }
        
        public string? Recommendations { get; set; }
        
        [Display(Name = "Management Response")]
        public string? ManagementResponse { get; set; }
        
        [Display(Name = "Scheduled By")]
        public string ScheduledBy { get; set; } = string.Empty;
        
        [Display(Name = "Scheduled On")]
        public DateTime ScheduledOn { get; set; }
        
        [Display(Name = "Conducted By")]
        public string? ConductedBy { get; set; }
        
        [Display(Name = "Conducted On")]
        public DateTime? ConductedOn { get; set; }
        
        [Display(Name = "Reviewed By")]
        public string? ReviewedBy { get; set; }
        
        [Display(Name = "Reviewed On")]
        public DateTime? ReviewedOn { get; set; }
        
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }
        
        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }
        
        [Display(Name = "Closed By")]
        public string? ClosedBy { get; set; }
        
        [Display(Name = "Closed On")]
        public DateTime? ClosedOn { get; set; }
        
        public string? ExportFilePath { get; set; }
        
        public AuditStatus Status { get; set; }
        
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
        public ICollection<AuditProduct>?  LinkedProducts  { get; set; }
        public ICollection<AuditProcess>?  LinkedProcesses { get; set; }
        public ICollection<AuditAsset>?    LinkedAssets    { get; set; }
        public ICollection<AuditMaterial>? LinkedMaterials { get; set; }
        public ICollection<AuditSupplier>? LinkedSuppliers { get; set; }
        // Reverse navigation — CAPAs that reference this audit via the CapaAudit junction
        public ICollection<CapaAudit>?     LinkedCapas     { get; set; }

        public ICollection<AuditAttachment>? Attachments { get; set; }
        public ICollection<AuditAuditLog>? AuditLogs { get; set; }

        public enum AuditStatus
        {
            Scheduled,
            [Display(Name = "In Progress")]
            InProgress,
            [Display(Name = "Report Draft")]
            ReportDraft,
            [Display(Name = "Under Review")]
            UnderReview,
            Approved,
            Closed
        }
        
        public enum AuditType
        {
            Internal,
            Supplier,
            Customer,
            Regulatory,
            Certification
        }
        
        public enum AuditScope
        {
            [Display(Name = "Quality System")]
            QualitySystem,
            Manufacturing,
            Laboratory,
            Warehouse,
            [Display(Name = "IT/Data Integrity")]
            ITDataIntegrity,
            Training,
            Equipment,
            Documentation,
            [Display(Name = "Full GMP")]
            FullGMP
        }
        
        public enum AuditRating
        {
            Satisfactory,
            Acceptable,
            [Display(Name = "Needs Improvement")]
            NeedsImprovement,
            Unsatisfactory,
            Critical
        }
    }
}
