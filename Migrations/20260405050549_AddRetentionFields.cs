using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Training",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Training",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuditRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CapaRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChangeRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ComplaintRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeviationRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DocumentRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LCRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaintenanceRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RIRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetentionHoldCooldownDays",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiskRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrainingRetentionYears",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Risk",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Risk",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "RegulatoryApplications",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "RegulatoryApplications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnOn",
                table: "RegulatoryApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Maintenance",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Maintenance",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "LicenceCertificationRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "LicenceCertificationRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedOn",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnOn",
                table: "LicenceCertificationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Deviation",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Deviation",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Complaint",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Complaint",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Change",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Change",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Capa",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Capa",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "Audit",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "Audit",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ObsoletedOn",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionExtended",
                table: "AppDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedBy",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedComments",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedOn",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionExtendedReason",
                table: "AppDocument",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovalReason",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionExtendedRemovedBy",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExtendedRemovedOn",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionHoldCooldownUntil",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisposalAuditLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Module = table.Column<string>(type: "TEXT", nullable: false),
                    RecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordReference = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    ActionBy = table.Column<string>(type: "TEXT", nullable: false),
                    ActionOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisposalAuditLog", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisposalAuditLog");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "AuditRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CapaRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ChangeRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ComplaintRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "DeviationRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "DocumentRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LCRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "MaintenanceRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "RIRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownDays",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "RiskRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "TrainingRetentionYears",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "WithdrawnOn",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "SuspendedOn",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "WithdrawnOn",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "ObsoletedOn",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtended",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedBy",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedComments",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedOn",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedReason",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovalReason",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedBy",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionExtendedRemovedOn",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "RetentionHoldCooldownUntil",
                table: "AppDocument");
        }
    }
}
