/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2024  C-realize IT Services SRL (https://www.c-realize.com)

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

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Models;
using OpenQMS.Models.LC;
using OpenQMS.Models.Navigation;
using OpenQMS.Models.Release;
using OpenQMS.Models.RI;

namespace OpenQMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser, AppRole, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<OpenQMS.Models.AppDocument> AppDocument { get; set; }
        public DbSet<OpenQMS.Models.DocumentAuditLog> DocumentAuditLog { get; set; }
        public DbSet<OpenQMS.Models.UserAuditLog> UserAuditLog { get; set; }
        public DbSet<OpenQMS.Models.Training> Training { get; set; }
        public DbSet<UserTraining> UserTraining { get; set; }
        public DbSet<DocumentTrainingRequirement> DocumentTrainingRequirement { get; set; }
        public DbSet<TrainingAttachment> TrainingAttachment { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Adverse Event <-> CAPA and Adverse Event <-> Complaint are two INDEPENDENT
            // one-way links: each entity keeps its own FK, kept in sync by the controllers.
            // The matching navigation types made EF try to merge each pair into a single
            // relationship and, with [ForeignKey] on both sides, split them with warning
            // 10612. Configure them explicitly as standalone relationships (no inverse
            // navigation). Mirrors the inferred model exactly — no schema change.
            modelBuilder.Entity<AdverseEvent>()
                .HasOne(a => a.LinkedCapa).WithMany().HasForeignKey(a => a.LinkedCapaId);
            modelBuilder.Entity<AdverseEvent>()
                .HasOne(a => a.LinkedComplaint).WithMany().HasForeignKey(a => a.LinkedComplaintId);
            modelBuilder.Entity<Capa>()
                .HasOne(c => c.LinkedAdverseEvent).WithMany().HasForeignKey(c => c.LinkedAdverseEventId);
            modelBuilder.Entity<Complaint>()
                .HasOne(c => c.LinkedAdverseEvent).WithMany().HasForeignKey(c => c.LinkedAEId);

            modelBuilder.Entity<UserTraining>().ToTable("UserTraining");
            modelBuilder.Entity<UserTraining>().HasKey(t => t.Id);
            modelBuilder.Entity<UserTraining>()
                .HasOne(ut => ut.Training)
                .WithMany(t => t.Trainees)
                .HasForeignKey(ut => ut.TrainingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserTraining>()
                .HasOne(ut => ut.Trainee)
                .WithMany(u => u.Trainings)
                .HasForeignKey(ut => ut.TraineeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TrainingAttachment>()
                .HasOne(a => a.UserTraining)
                .WithMany(ut => ut.Attachments)
                .HasForeignKey(a => a.UserTrainingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DocumentTrainingRequirement>()
                .HasOne(r => r.Document)
                .WithMany()
                .HasForeignKey(r => r.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DocumentTrainingRequirement>()
                .HasOne(r => r.Training)
                .WithMany()
                .HasForeignKey(r => r.TrainingId)
                .OnDelete(DeleteBehavior.Restrict);

            // Supplier junction table composite keys
            modelBuilder.Entity<SupplierMaterial>().HasKey(t => new { t.SupplierId, t.MaterialId });
            modelBuilder.Entity<SupplierProduct>().HasKey(t => new { t.SupplierId, t.ProductId });
            modelBuilder.Entity<SupplierService>().HasKey(t => new { t.SupplierId, t.ServiceId });

            // Change junction table composite keys
            modelBuilder.Entity<ChangeProduct>().HasKey(t => new { t.ChangeId, t.ProductId });
            modelBuilder.Entity<ChangeProcess>().HasKey(t => new { t.ChangeId, t.ProcessId });
            modelBuilder.Entity<ChangeAsset>().HasKey(t => new { t.ChangeId, t.AssetId });
            modelBuilder.Entity<ChangeMaterial>().HasKey(t => new { t.ChangeId, t.MaterialId });
            modelBuilder.Entity<ChangeSupplier>().HasKey(t => new { t.ChangeId, t.SupplierId });
            modelBuilder.Entity<ChangeCapa>().HasKey(t => new { t.ChangeId, t.CapaId });

            // Change action-plan items — cascade with the parent change; edited/deleted freely
            // (not an audit table, so append-only triggers do not apply).
            modelBuilder.Entity<ChangeActionPlanItem>()
                .HasOne(i => i.Change)
                .WithMany(c => c.ActionPlanItems)
                .HasForeignKey(i => i.ChangeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Capa junction table composite keys
            modelBuilder.Entity<CapaProduct>().HasKey(t => new { t.CapaId, t.ProductId });
            modelBuilder.Entity<CapaProcess>().HasKey(t => new { t.CapaId, t.ProcessId });
            modelBuilder.Entity<CapaAsset>().HasKey(t => new { t.CapaId, t.AssetId });
            modelBuilder.Entity<CapaMaterial>().HasKey(t => new { t.CapaId, t.MaterialId });
            modelBuilder.Entity<CapaSupplier>().HasKey(t => new { t.CapaId, t.SupplierId });
            modelBuilder.Entity<CapaDeviation>().HasKey(t => new { t.CapaId, t.DeviationId });
            modelBuilder.Entity<CapaComplaint>().HasKey(t => new { t.CapaId, t.ComplaintId });
            modelBuilder.Entity<CapaAudit>().HasKey(t => new { t.CapaId, t.AuditId });

            // Deviation junction table composite keys
            modelBuilder.Entity<DeviationProduct>().HasKey(t => new { t.DeviationId, t.ProductId });
            modelBuilder.Entity<DeviationProcess>().HasKey(t => new { t.DeviationId, t.ProcessId });
            modelBuilder.Entity<DeviationAsset>().HasKey(t => new { t.DeviationId, t.AssetId });
            modelBuilder.Entity<DeviationMaterial>().HasKey(t => new { t.DeviationId, t.MaterialId });
            modelBuilder.Entity<DeviationSupplier>().HasKey(t => new { t.DeviationId, t.SupplierId });

            // Complaint junction table composite keys
            modelBuilder.Entity<ComplaintProduct>().HasKey(t => new { t.ComplaintId, t.ProductId });
            modelBuilder.Entity<ComplaintProcess>().HasKey(t => new { t.ComplaintId, t.ProcessId });
            modelBuilder.Entity<ComplaintMaterial>().HasKey(t => new { t.ComplaintId, t.MaterialId });
            modelBuilder.Entity<ComplaintSupplier>().HasKey(t => new { t.ComplaintId, t.SupplierId });

            // Audit junction table composite keys
            modelBuilder.Entity<AuditProduct>().HasKey(t => new { t.AuditId, t.ProductId });
            modelBuilder.Entity<AuditProcess>().HasKey(t => new { t.AuditId, t.ProcessId });
            modelBuilder.Entity<AuditAsset>().HasKey(t => new { t.AuditId, t.AssetId });
            modelBuilder.Entity<AuditMaterial>().HasKey(t => new { t.AuditId, t.MaterialId });
            modelBuilder.Entity<AuditSupplier>().HasKey(t => new { t.AuditId, t.SupplierId });

            // Risk junction table composite keys
            modelBuilder.Entity<RiskProduct>().HasKey(t => new { t.RiskId, t.ProductId });
            modelBuilder.Entity<RiskProcess>().HasKey(t => new { t.RiskId, t.ProcessId });
            modelBuilder.Entity<RiskAsset>().HasKey(t => new { t.RiskId, t.AssetId });
            modelBuilder.Entity<RiskMaterial>().HasKey(t => new { t.RiskId, t.MaterialId });
            modelBuilder.Entity<RiskSupplier>().HasKey(t => new { t.RiskId, t.SupplierId });

            // Maintenance junction table composite keys
            modelBuilder.Entity<MaintenanceDeviation>().HasKey(t => new { t.MaintenanceId, t.DeviationId });
            modelBuilder.Entity<MaintenanceCapa>().HasKey(t => new { t.MaintenanceId, t.CapaId });

            // Master data related document composite keys
            modelBuilder.Entity<AssetLinkedDocument>().HasKey(t => new { t.AssetId, t.AppDocumentId });
            modelBuilder.Entity<CustomerLinkedDocument>().HasKey(t => new { t.CustomerId, t.AppDocumentId });
            modelBuilder.Entity<MaterialLinkedDocument>().HasKey(t => new { t.MaterialId, t.AppDocumentId });
            modelBuilder.Entity<ProcessLinkedDocument>().HasKey(t => new { t.ProcessId, t.AppDocumentId });
            modelBuilder.Entity<ProductLinkedDocument>().HasKey(t => new { t.ProductId, t.AppDocumentId });
            modelBuilder.Entity<ServiceLinkedDocument>().HasKey(t => new { t.ServiceId, t.AppDocumentId });

            // Maintenance self-reference (recurring chain) — no cascade to avoid cycles
            modelBuilder.Entity<Maintenance>()
                .HasOne(m => m.ParentMaintenance)
                .WithMany()
                .HasForeignKey(m => m.ParentMaintenanceId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProductStrength cascade delete
            modelBuilder.Entity<ProductStrength>()
                .HasOne(s => s.Product)
                .WithMany(p => p.Strengths)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // ProductSpecLine cascade delete
            modelBuilder.Entity<ProductSpecLine>()
                .HasOne(s => s.Product)
                .WithMany(p => p.Specifications)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reference data junction table composite keys
            modelBuilder.Entity<AssetProcess>().HasKey(t => new { t.AssetId, t.ProcessId });
            modelBuilder.Entity<MaterialProcess>().HasKey(t => new { t.MaterialId, t.ProcessId });
            modelBuilder.Entity<MaterialProduct>().HasKey(t => new { t.MaterialId, t.ProductId });
            modelBuilder.Entity<ProcessProduct>().HasKey(t => new { t.ProcessId, t.ProductId });

            // Pharmacovigilance: self-referencing AdverseEvent (Follow-Up Reports)
            modelBuilder.Entity<AdverseEvent>()
                .HasMany(a => a.FollowUpReports)
                .WithOne(a => a.ParentAE)
                .HasForeignKey(a => a.ParentAEId)
                .OnDelete(DeleteBehavior.Restrict);

            // Audit log relationships — ParentId is a non-conventional FK name so EF Core
            // cannot infer it automatically; must be declared explicitly.
            modelBuilder.Entity<Deviation>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Change>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Complaint>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Risk>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Audit>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Maintenance>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Supplier>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PVPeriodicReport>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PVLiteratureSearch>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PVSignalReview>()
                .HasMany(d => d.AuditLogs)
                .WithOne()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReleaseTestingLab>()
                .HasOne(t => t.ReleaseRecord)
                .WithMany(r => r.TestingLabs)
                .HasForeignKey(t => t.ReleaseRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            // Attachments are soft-deleted (row + file retained, with reason). Hide deleted
            // ones from every query and navigation Include; use IgnoreQueryFilters() to see them.
            modelBuilder.Entity<DeviationAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<CapaAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<ChangeAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<ComplaintAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<AuditAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<RiskAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<MaintenanceAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<AssetDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<MaterialDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<ProcessDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<ProductDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<ServiceDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<CustomerDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<SupplierDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<RIDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<ReleaseDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<LCDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<AEAttachment>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<PSURAttachment>().HasQueryFilter(a => !a.IsDeleted);
        }

        public DbSet<OpenQMS.Models.Change>? Change { get; set; }
        public DbSet<OpenQMS.Models.Capa>? Capa { get; set; }
        public DbSet<OpenQMS.Models.Deviation>? Deviation { get; set; }
        public DbSet<OpenQMS.Models.Product> Product { get; set; }
        public DbSet<OpenQMS.Models.Asset> Asset { get; set; }
        public DbSet<OpenQMS.Models.Process> Process { get; set; }
        public DbSet<OpenQMS.Models.Material> Material { get; set; }
        public DbSet<ChangeAttachment> ChangeAttachment { get; set; }
        public DbSet<ChangeAuditLog> ChangeAuditLog { get; set; }
        public DbSet<ChangeActionPlanItem> ChangeActionPlanItem { get; set; }
        public DbSet<CapaAttachment> CapaAttachment { get; set; }
        public DbSet<CapaAuditLog> CapaAuditLog { get; set; }
        public DbSet<DeviationAttachment> DeviationAttachment { get; set; }
        public DbSet<DeviationAuditLog> DeviationAuditLog { get; set; }
        public DbSet<Complaint> Complaint { get; set; }
        public DbSet<ComplaintAttachment> ComplaintAttachment { get; set; }
        public DbSet<ComplaintAuditLog> ComplaintAuditLog { get; set; }
        public DbSet<Risk> Risk { get; set; }
        public DbSet<RiskAttachment> RiskAttachment { get; set; }
        public DbSet<RiskAuditLog> RiskAuditLog { get; set; }
        public DbSet<Audit> Audit { get; set; }
        public DbSet<AuditAttachment> AuditAttachment { get; set; }
        public DbSet<AuditAuditLog> AuditAuditLog { get; set; }
        public DbSet<Maintenance> Maintenance { get; set; }
        public DbSet<MaintenanceAttachment> MaintenanceAttachment { get; set; }
        public DbSet<MaintenanceAuditLog> MaintenanceAuditLog { get; set; }
        public DbSet<Service> Service { get; set; }
        public DbSet<ServiceDocument> ServiceDocument { get; set; }

        public DbSet<ProcessDocument> ProcessDocument { get; set; }
        public DbSet<AssetDocument> AssetDocument { get; set; }
        public DbSet<MaterialDocument> MaterialDocument { get; set; }
        public DbSet<ProductDocument> ProductDocument { get; set; }
        public DbSet<ProductStrength> ProductStrengths { get; set; }
        public DbSet<ProductSpecLine> ProductSpecLines { get; set; }

        // Customer module
        public DbSet<Customer> Customer { get; set; }
        public DbSet<CustomerDocument> CustomerDocument { get; set; }

        // Supplier module
        public DbSet<Supplier> Supplier { get; set; }
        public DbSet<SupplierAddress> SupplierAddress { get; set; }
        public DbSet<SupplierAuditLog> SupplierAuditLog { get; set; }
        public DbSet<SupplierDocument> SupplierDocument { get; set; }
        public DbSet<SupplierQualityRecord> SupplierQualityRecord { get; set; }
        public DbSet<SupplierReview> SupplierReview { get; set; }
        public DbSet<SupplierMaterial> SupplierMaterial { get; set; }
        public DbSet<SupplierProduct> SupplierProduct { get; set; }
        public DbSet<SupplierService> SupplierService { get; set; }
        public DbSet<SupplierSnapshot> SupplierSnapshot { get; set; }
        public DbSet<SupplierSnapshotDocument> SupplierSnapshotDocument { get; set; }
        public DbSet<SupplierSnapshotQualityRecord> SupplierSnapshotQualityRecord { get; set; }
        public DbSet<SupplierSnapshotAddress> SupplierSnapshotAddress { get; set; }

        // Pharmacovigilance module
        public DbSet<AdverseEvent> AdverseEvent { get; set; }
        public DbSet<AEReporter> AEReporter { get; set; }
        public DbSet<AEPatient> AEPatient { get; set; }
        public DbSet<AESuspectProduct> AESuspectProduct { get; set; }
        public DbSet<AERegulatoryReport> AERegulatoryReport { get; set; }
        public DbSet<AEAttachment> AEAttachment { get; set; }
        public DbSet<AEAuditLog> AEAuditLog { get; set; }
        public DbSet<PVLiteratureSearch> PVLiteratureSearch { get; set; }
        public DbSet<PVLiteratureAuditLog> PVLiteratureAuditLog { get; set; }
        public DbSet<PVPeriodicReport>  PVPeriodicReport  { get; set; }
        public DbSet<PSURAttachment>    PSURAttachments   { get; set; }
        public DbSet<PVPsurAuditLog> PVPsurAuditLog { get; set; }
        public DbSet<PVSignalReview> PVSignalReview { get; set; }
        public DbSet<PVSignalAuditLog> PVSignalAuditLog { get; set; }

        // ── RegIntel Module ────────────────────────────────────────────────
        public DbSet<RegulatoryApplication>    RegulatoryApplications     { get; set; }
        public DbSet<RIJurisdiction>           RIJurisdictions            { get; set; }
        public DbSet<RIApplicationType>        RIApplicationTypes         { get; set; }
        public DbSet<RIMilestone>              RIMilestones               { get; set; }
        public DbSet<RIConditionOfApproval>    RIConditionsOfApproval     { get; set; }
        public DbSet<RIVariation>              RIVariations               { get; set; }
        public DbSet<RIAccessPathwayRecord>    RIAccessPathwayRecords     { get; set; }
        public DbSet<RISixMonthlySupplyReport> RISixMonthlySupplyReports  { get; set; }
        public DbSet<RIDocument>               RIDocuments                { get; set; }
        public DbSet<RIAuditLog>               RIAuditLogs                { get; set; }
        public DbSet<RIApprovalSnapshot>       RIApprovalSnapshots        { get; set; }

        // ── LicCert Module ────────────────────────────────────────────────
        public DbSet<LicenceCertificationRecord> LicenceCertificationRecords { get; set; }
        public DbSet<LCJurisdiction>             LCJurisdictions             { get; set; }
        public DbSet<LCLicenceType>              LCLicenceTypes              { get; set; }
        public DbSet<LCCondition>                LCConditions                { get; set; }
        public DbSet<LCDocument>                 LCDocuments                 { get; set; }
        public DbSet<LCDependency>               LCDependencies              { get; set; }
        public DbSet<LCAuditLog>                 LCAuditLogs                 { get; set; }
        public DbSet<LCApprovalSnapshot>         LCApprovalSnapshots         { get; set; }

        // ── Release Module ────────────────────────────────────────────────
        public DbSet<ReleaseRecord>          ReleaseRecords          { get; set; }
        public DbSet<ReleaseChecklistItem>   ReleaseChecklistItems   { get; set; }
        public DbSet<ReleaseTestResult>      ReleaseTestResults      { get; set; }
        public DbSet<ReleaseDocument>        ReleaseDocuments        { get; set; }
        public DbSet<ReleaseCertificate>     ReleaseCertificates     { get; set; }
        public DbSet<ReleaseAuditEntry>      ReleaseAuditEntries     { get; set; }
        public DbSet<ReleaseCrossReference>  ReleaseCrossReferences  { get; set; }
        public DbSet<ReleaseTestingLab>      ReleaseTestingLabs      { get; set; }

        // ── Master Data Audit Log ─────────────────────────────────────────
        public DbSet<MasterDataAuditLog> MasterDataAuditLog { get; set; }

        // ── Settings Module ───────────────────────────────────────────────
        public DbSet<SystemSettings>    SystemSettings    { get; set; }
        public DbSet<SettingsAuditLog>  SettingsAuditLog  { get; set; }
        public DbSet<PasswordHistory>   PasswordHistory   { get; set; }

        // ── Security ─────────────────────────────────────────────────────
        public DbSet<LoginAuditLog>     LoginAuditLog     { get; set; }
        public DbSet<ActiveSession>     ActiveSession     { get; set; }

        // ── Gap 13: Retention & Disposal ─────────────────────────────────
        public DbSet<DisposalAuditLog>  DisposalAuditLog  { get; set; }

        // ── Many-to-many junction tables ──────────────────────────────────
        // Change
        public DbSet<ChangeProduct>  ChangeProduct  { get; set; }
        public DbSet<ChangeProcess>  ChangeProcess  { get; set; }
        public DbSet<ChangeAsset>    ChangeAsset    { get; set; }
        public DbSet<ChangeMaterial> ChangeMaterial { get; set; }
        public DbSet<ChangeSupplier> ChangeSupplier { get; set; }
        public DbSet<ChangeCapa>     ChangeCapa     { get; set; }
        // Capa
        public DbSet<CapaProduct>   CapaProduct   { get; set; }
        public DbSet<CapaProcess>   CapaProcess   { get; set; }
        public DbSet<CapaAsset>     CapaAsset     { get; set; }
        public DbSet<CapaMaterial>  CapaMaterial  { get; set; }
        public DbSet<CapaSupplier>  CapaSupplier  { get; set; }
        public DbSet<CapaDeviation> CapaDeviation { get; set; }
        public DbSet<CapaComplaint> CapaComplaint { get; set; }
        public DbSet<CapaAudit>     CapaAudit     { get; set; }
        // Deviation
        public DbSet<DeviationProduct>  DeviationProduct  { get; set; }
        public DbSet<DeviationProcess>  DeviationProcess  { get; set; }
        public DbSet<DeviationAsset>    DeviationAsset    { get; set; }
        public DbSet<DeviationMaterial> DeviationMaterial { get; set; }
        public DbSet<DeviationSupplier> DeviationSupplier { get; set; }
        // Complaint
        public DbSet<ComplaintProduct>  ComplaintProduct  { get; set; }
        public DbSet<ComplaintProcess>  ComplaintProcess  { get; set; }
        public DbSet<ComplaintMaterial> ComplaintMaterial { get; set; }
        public DbSet<ComplaintSupplier> ComplaintSupplier { get; set; }
        // Audit
        public DbSet<AuditProduct>  AuditProduct  { get; set; }
        public DbSet<AuditProcess>  AuditProcess  { get; set; }
        public DbSet<AuditAsset>    AuditAsset    { get; set; }
        public DbSet<AuditMaterial> AuditMaterial { get; set; }
        public DbSet<AuditSupplier> AuditSupplier { get; set; }
        // Risk
        public DbSet<RiskProduct>  RiskProduct  { get; set; }
        public DbSet<RiskProcess>  RiskProcess  { get; set; }
        public DbSet<RiskAsset>    RiskAsset    { get; set; }
        public DbSet<RiskMaterial> RiskMaterial { get; set; }
        public DbSet<RiskSupplier> RiskSupplier { get; set; }
        // Maintenance
        public DbSet<MaintenanceDeviation> MaintenanceDeviation { get; set; }
        public DbSet<MaintenanceCapa>      MaintenanceCapa      { get; set; }
        // Reference data
        public DbSet<AssetProcess>    AssetProcess    { get; set; }
        public DbSet<MaterialProcess> MaterialProcess { get; set; }
        public DbSet<MaterialProduct> MaterialProduct { get; set; }
        public DbSet<ProcessProduct>  ProcessProduct  { get; set; }
        // Master data related documents
        public DbSet<AssetLinkedDocument>    AssetLinkedDocument    { get; set; }
        public DbSet<CustomerLinkedDocument> CustomerLinkedDocument { get; set; }
        public DbSet<MaterialLinkedDocument> MaterialLinkedDocument { get; set; }
        public DbSet<ProcessLinkedDocument>  ProcessLinkedDocument  { get; set; }
        public DbSet<ProductLinkedDocument>  ProductLinkedDocument  { get; set; }
        public DbSet<ServiceLinkedDocument>  ServiceLinkedDocument  { get; set; }
        public DbSet<OpenQMS.Models.Release.ReleaseCertificationSetting> ReleaseCertificationSettings { get; set; }
        public DbSet<OpenQMS.Models.Release.ReleaseCertSettingAuditLog> ReleaseCertSettingAuditLogs { get; set; }
    }
}
