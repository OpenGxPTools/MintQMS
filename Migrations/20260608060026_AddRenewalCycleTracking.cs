using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalCycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalCycle",
                table: "RIDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewalCycle",
                table: "RegulatoryApplications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RenewalCycle",
                table: "LicenceCertificationRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCycle",
                table: "LCDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LCApprovalSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicenceCertificationRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    RenewalCycle = table.Column<int>(type: "INTEGER", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TakenByUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LCApprovalSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LCApprovalSnapshots_LicenceCertificationRecords_LicenceCertificationRecordId",
                        column: x => x.LicenceCertificationRecordId,
                        principalTable: "LicenceCertificationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RIApprovalSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegulatoryApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    RenewalCycle = table.Column<int>(type: "INTEGER", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TakenByUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIApprovalSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIApprovalSnapshots_RegulatoryApplications_RegulatoryApplicationId",
                        column: x => x.RegulatoryApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LCApprovalSnapshots_LicenceCertificationRecordId",
                table: "LCApprovalSnapshots",
                column: "LicenceCertificationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_RIApprovalSnapshots_RegulatoryApplicationId",
                table: "RIApprovalSnapshots",
                column: "RegulatoryApplicationId");

            // Snapshot tables are immutable — block UPDATE and DELETE
            foreach (var table in new[] { "LCApprovalSnapshots", "RIApprovalSnapshots" })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_no_update
                    BEFORE UPDATE ON "{table}"
                    BEGIN
                        SELECT RAISE(ABORT, '{table} records are immutable and cannot be modified.');
                    END;
                    """);

                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_no_delete
                    BEFORE DELETE ON "{table}"
                    BEGIN
                        SELECT RAISE(ABORT, '{table} records are immutable and cannot be deleted.');
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LCApprovalSnapshots");

            migrationBuilder.DropTable(
                name: "RIApprovalSnapshots");

            migrationBuilder.DropColumn(
                name: "ApprovalCycle",
                table: "RIDocuments");

            migrationBuilder.DropColumn(
                name: "RenewalCycle",
                table: "RegulatoryApplications");

            migrationBuilder.DropColumn(
                name: "RenewalCycle",
                table: "LicenceCertificationRecords");

            migrationBuilder.DropColumn(
                name: "ApprovalCycle",
                table: "LCDocuments");
        }
    }
}
