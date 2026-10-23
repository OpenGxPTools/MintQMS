using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "SupplierDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "SupplierDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "SupplierDocument",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SupplierDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ServiceDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "ServiceDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "ServiceDocument",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ServiceDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "RiskAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "RiskAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "RiskAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RiskAttachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "RIDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "RIDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "RIDocuments",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RIDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ReleaseDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "ReleaseDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "ReleaseDocuments",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ReleaseDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "PSURAttachments",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ProductDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "ProductDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "ProductDocument",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ProcessDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "ProcessDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "ProcessDocument",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProcessDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "MaterialDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "MaterialDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "MaterialDocument",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MaterialDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "MaintenanceAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "MaintenanceAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "MaintenanceAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MaintenanceAttachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "LCDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "LCDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "LCDocuments",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "DeviationAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "DeviationAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "DeviationAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "DeviationAttachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "CustomerDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "CustomerDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "CustomerDocument",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CustomerDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ComplaintAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "ComplaintAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "ComplaintAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ComplaintAttachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ChangeAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "ChangeAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "ChangeAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChangeAttachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "CapaAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "CapaAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "CapaAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CapaAttachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AuditAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "AuditAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "AuditAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AuditAttachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AssetDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "AssetDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "AssetDocument",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AssetDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "AEAttachment",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "SupplierDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "SupplierDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "SupplierDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SupplierDocument");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ServiceDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "ServiceDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "ServiceDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ServiceDocument");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "RiskAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "RiskAttachment");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "RiskAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RiskAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "RIDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "RIDocuments");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "RIDocuments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RIDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ReleaseDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "ReleaseDocuments");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "ReleaseDocuments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ReleaseDocuments");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "PSURAttachments");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ProductDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "ProductDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "ProductDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductDocument");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ProcessDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "ProcessDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "ProcessDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProcessDocument");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "MaterialDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "MaterialDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "MaterialDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MaterialDocument");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "MaintenanceAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "MaintenanceAttachment");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "MaintenanceAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MaintenanceAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "LCDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "LCDocuments");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "LCDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "DeviationAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "DeviationAttachment");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "DeviationAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "DeviationAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "CustomerDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "CustomerDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "CustomerDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CustomerDocument");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ComplaintAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "ComplaintAttachment");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "ComplaintAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ComplaintAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ChangeAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "ChangeAttachment");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "ChangeAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChangeAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "CapaAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "CapaAttachment");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "CapaAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CapaAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AuditAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "AuditAttachment");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "AuditAttachment");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AuditAttachment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AssetDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "AssetDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "AssetDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AssetDocument");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "AEAttachment");
        }
    }
}
