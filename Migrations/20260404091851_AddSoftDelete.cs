using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Training",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Supplier",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Supplier",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Supplier",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Service",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Service",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Service",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Risk",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Product",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Process",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Process",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Process",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Material",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Material",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Material",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Maintenance",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Deviation",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Customer",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Customer",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Customer",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Complaint",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Change",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Change",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Capa",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Capa",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Audit",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Audit",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Asset",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Asset",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Asset",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AppDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Process");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Process");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Process");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Material");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Material");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Material");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Asset");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Asset");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Asset");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AppDocument");
        }
    }
}
