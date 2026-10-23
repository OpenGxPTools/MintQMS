using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceRecurringAndSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentMaintenanceId",
                table: "Maintenance",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedBy",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedOn",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "Maintenance",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_ParentMaintenanceId",
                table: "Maintenance",
                column: "ParentMaintenanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Maintenance_Maintenance_ParentMaintenanceId",
                table: "Maintenance",
                column: "ParentMaintenanceId",
                principalTable: "Maintenance",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Maintenance_Maintenance_ParentMaintenanceId",
                table: "Maintenance");

            migrationBuilder.DropIndex(
                name: "IX_Maintenance_ParentMaintenanceId",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "ParentMaintenanceId",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "SuspendedBy",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "SuspendedOn",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "Maintenance");
        }
    }
}
