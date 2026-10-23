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
    public class Risk
    {
        public int Id { get; set; }
        
        [Display(Name = "Risk ID")]
        public string RiskId { get; set; } = string.Empty;
        
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
        
        public string Title { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        [Display(Name = "Risk Category")]
        public RiskCategory Category { get; set; }
        
        [Display(Name = "Severity (1-5)")]
        [Range(1, 5)]
        public int Severity { get; set; }
        
        [Display(Name = "Probability (1-5)")]
        [Range(1, 5)]
        public int Probability { get; set; }
        
        [Display(Name = "Detectability (1-5)")]
        [Range(1, 5)]
        public int Detectability { get; set; }
        
        [Display(Name = "Initial RPN")]
        public int InitialRPN { get; set; }
        
        [Display(Name = "Identified By")]
        public string IdentifiedBy { get; set; } = string.Empty;
        
        [Display(Name = "Identified On")]
        public DateTime IdentifiedOn { get; set; }
        
        [Display(Name = "Risk Owner")]
        public string? RiskOwner { get; set; }
        
        [Display(Name = "Assessed By")]
        public string? AssessedBy { get; set; }
        
        [Display(Name = "Assessed On")]
        public DateTime? AssessedOn { get; set; }
        
        [Display(Name = "Mitigation Strategy")]
        public string? MitigationStrategy { get; set; }
        
        [Display(Name = "Target Completion Date")]
        public DateTime? TargetCompletionDate { get; set; }
        
        [Display(Name = "Actual Completion Date")]
        public DateTime? ActualCompletionDate { get; set; }
        
        [Display(Name = "Mitigated By")]
        public string? MitigatedBy { get; set; }
        
        [Display(Name = "Mitigated On")]
        public DateTime? MitigatedOn { get; set; }
        
        [Display(Name = "Residual Severity (1-5)")]
        [Range(1, 5)]
        public int? ResidualSeverity { get; set; }
        
        [Display(Name = "Residual Probability (1-5)")]
        [Range(1, 5)]
        public int? ResidualProbability { get; set; }
        
        [Display(Name = "Residual Detectability (1-5)")]
        [Range(1, 5)]
        public int? ResidualDetectability { get; set; }
        
        [Display(Name = "Residual RPN")]
        public int? ResidualRPN { get; set; }
        
        [Display(Name = "Risk Acceptable")]
        public bool? RiskAcceptable { get; set; }
        
        public string? Justification { get; set; }
        
        [Display(Name = "Reviewed By")]
        public string? ReviewedBy { get; set; }

        [Display(Name = "Reviewed On")]
        public DateTime? ReviewedOn { get; set; }

        [Display(Name = "Accepted By")]
        public string? AcceptedBy { get; set; }

        [Display(Name = "Accepted On")]
        public DateTime? AcceptedOn { get; set; }

        [Display(Name = "Assessment Notes")]
        public string? AssessmentNotes { get; set; }

        [Display(Name = "Review Conclusion")]
        public string? ReviewConclusion { get; set; }

        [Display(Name = "Monitoring Plan")]
        public string? MonitoringPlan { get; set; }

        [Display(Name = "Next Review Date")]
        public DateTime? NextReviewDate { get; set; }

        [Display(Name = "Closed By")]
        public string? ClosedBy { get; set; }

        [Display(Name = "Closed On")]
        public DateTime? ClosedOn { get; set; }
        
        public string? ExportFilePath { get; set; }
        
        public RiskStatus Status { get; set; }
        
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
        public ICollection<RiskProduct>?  LinkedProducts  { get; set; }
        public ICollection<RiskProcess>?  LinkedProcesses { get; set; }
        public ICollection<RiskAsset>?    LinkedAssets    { get; set; }
        public ICollection<RiskMaterial>? LinkedMaterials { get; set; }
        public ICollection<RiskSupplier>? LinkedSuppliers { get; set; }

        public ICollection<RiskAttachment>? Attachments { get; set; }
        public ICollection<RiskAuditLog>? AuditLogs { get; set; }

        public enum RiskStatus
        {
            Identification    = 0,
            Assessment        = 1,  // legacy — treated as Mitigation-ready (pre-new-workflow records)
            Mitigation        = 2,
            Review            = 3,  // legacy — treated as PendingReview (pre-new-workflow records)
            Closed            = 4,
            [Display(Name = "Pending Assessment")]
            PendingAssessment = 5,
            [Display(Name = "Pending Review")]
            PendingReview     = 6
        }
        
        public enum RiskCategory
        {
            [Display(Name = "Product Quality")]
            ProductQuality,
            [Display(Name = "Patient Safety")]
            PatientSafety,
            [Display(Name = "Data Integrity")]
            DataIntegrity,
            [Display(Name = "Supply Chain")]
            SupplyChain,
            [Display(Name = "Regulatory Compliance")]
            RegulatoryCompliance,
            [Display(Name = "Equipment")]
            Equipment,
            [Display(Name = "Personnel")]
            Personnel,
            [Display(Name = "Environmental")]
            Environmental
        }
    }
}
