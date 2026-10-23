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

namespace OpenQMS.Models
{
    public class AppDocument
    {
        public int Id { get; set; }
        [Display(Name = "ID")]
        public string DocumentId { get; set; }
        public string? GeneratedFrom { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Version { get; set; }
        public byte[]? Content { get; set; }
        public string? FilePath { get; set; }
        public string FileType { get; set; }
        public string FileExtension { get; set; }
        [Display(Name = "Authored By")]
        public string AuthoredBy { get; set; }
        [Display(Name = "Authored On")]
        public DateTime AuthoredOn { get; set; }
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }
        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }
        public bool IsLocked { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        [Display(Name = "Obsoleted On")]
        public DateTime? ObsoletedOn { get; set; }
        public string? ExportFilePath { get; set; }
        public DocumentStatus Status { get; set; }
        public DocumentCategory? Category { get; set; }
        public DocumentType? DocType { get; set; }
        [Display(Name = "Issued On")]
        public DateTime? IssuedOn { get; set; }
        [Display(Name = "Issued By")]
        public string? IssuedBy { get; set; }
        [Display(Name = "Issue Commentary")]
        public string? IssueCommentary { get; set; }
        [Display(Name = "Reason for Change")]
        public string? ReasonForChange { get; set; }
        [Display(Name = "Next Review Date")]
        public DateTime? ReviewDueDate { get; set; }
        [Display(Name = "Periodic Review Required")]
        public bool PeriodicReviewRequired { get; set; } = false;
        [Display(Name = "Training Required")]
        public bool TrainingRequired { get; set; } = true;
        public string? FileHash { get; set; }
        public List<DocumentAuditLog>? AuditLogs { get; set; }

        // ── External Document Provenance ──────────────────────────────────────
        public bool      IsExternal           { get; set; } = false;
        public string?   ExternalSource       { get; set; }
        public string?   ExternalVersion      { get; set; }
        public string?   ExternalAuthorName   { get; set; }
        public DateTime? ExternalAuthoredOn   { get; set; }
        public string?   ExternalApproverName { get; set; }
        public DateTime? ExternalApprovedOn   { get; set; }

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

        public enum DocumentStatus
        {
            Draft,
            Approved,
            Rejected,
            Obsolete,
            [Display(Name = "Under Review")]
            UnderReview,
            Effective
        }

        public enum DocumentCategory
        {
            [Display(Name = "Quality System")]  QualitySystem,
            [Display(Name = "Technical")]       Technical,
            [Display(Name = "Regulatory")]      Regulatory,
            [Display(Name = "Agreements")]      Agreements,
            [Display(Name = "Training")]        Training,
            [Display(Name = "Records")]         Records
        }

        public enum DocumentType
        {
            // Quality System
            [Display(Name = "Quality Manual")]                      QualityManual,
            [Display(Name = "Policy")]                              Policy,
            [Display(Name = "Standard Operating Procedure (SOP)")] StandardOperatingProcedure,
            [Display(Name = "Work Instruction (WI)")]               WorkInstruction,
            [Display(Name = "Form")]                                Form,
            [Display(Name = "Template")]                            Template,
            [Display(Name = "Guideline")]                           Guideline,
            [Display(Name = "Site Master File")]                    SiteMasterFile,
            [Display(Name = "Management Review")]                   ManagementReview,
            [Display(Name = "Product Quality Review (PQR)")]        ProductQualityReview,
            [Display(Name = "Risk Assessment")]                     RiskAssessment,
            // Technical
            [Display(Name = "Specification")]                       Specification,
            [Display(Name = "Protocol")]                            Protocol,
            [Display(Name = "Validation Report")]                   ValidationReport,
            [Display(Name = "Study Report")]                        StudyReport,
            [Display(Name = "Test Report")]                         TestReport,
            [Display(Name = "Master Batch Record")]                 MasterBatchRecord,
            [Display(Name = "Test Method")]                         TestMethod,
            // Regulatory
            [Display(Name = "Regulatory Submission")]               RegulatorySubmission,
            [Display(Name = "Product Dossier")]                     ProductDossier,
            [Display(Name = "Regulatory Correspondence")]           RegulatoryCorrespondence,
            [Display(Name = "Labelling")]                           Labelling,
            // Agreements
            [Display(Name = "Quality Agreement")]                   QualityAgreement,
            [Display(Name = "Supply Agreement")]                    SupplyAgreement,
            [Display(Name = "Confidentiality Agreement")]           ConfidentialityAgreement,
            // Training
            [Display(Name = "Training Material")]                   TrainingMaterial,
            [Display(Name = "Competency Assessment")]               CompetencyAssessment,
            // Records
            [Display(Name = "Certificate of Analysis (CoA)")]      CertificateOfAnalysis,
            [Display(Name = "Audit Report")]                        AuditReport,
            [Display(Name = "Meeting Minutes")]                     MeetingMinutes
        }
    }
}
