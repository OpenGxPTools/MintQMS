using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AuditId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AuditLocation = table.Column<string>(type: "TEXT", nullable: true),
                    LeadAuditor = table.Column<string>(type: "TEXT", nullable: true),
                    AuditTeam = table.Column<string>(type: "TEXT", nullable: true),
                    Auditee = table.Column<string>(type: "TEXT", nullable: true),
                    AuditeeContact = table.Column<string>(type: "TEXT", nullable: true),
                    FindingsSummary = table.Column<string>(type: "TEXT", nullable: true),
                    CriticalFindings = table.Column<int>(type: "INTEGER", nullable: false),
                    MajorFindings = table.Column<int>(type: "INTEGER", nullable: false),
                    MinorFindings = table.Column<int>(type: "INTEGER", nullable: false),
                    Observations = table.Column<int>(type: "INTEGER", nullable: false),
                    OverallRating = table.Column<int>(type: "INTEGER", nullable: true),
                    CAPARequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    LinkedCapaId = table.Column<int>(type: "INTEGER", nullable: true),
                    FollowUpDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FollowUpCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuditPlan = table.Column<string>(type: "TEXT", nullable: true),
                    AuditReport = table.Column<string>(type: "TEXT", nullable: true),
                    Recommendations = table.Column<string>(type: "TEXT", nullable: true),
                    ManagementResponse = table.Column<string>(type: "TEXT", nullable: true),
                    ScheduledBy = table.Column<string>(type: "TEXT", nullable: false),
                    ScheduledOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConductedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ConductedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExportFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audit_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Audit_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Audit_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Audit_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuditAttachment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditAttachment_Audit_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_AssetId",
                table: "Audit",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_MaterialId",
                table: "Audit",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_ProcessId",
                table: "Audit",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_ProductId",
                table: "Audit",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditAttachment_AuditId",
                table: "AuditAttachment",
                column: "AuditId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditAttachment");

            migrationBuilder.DropTable(
                name: "Audit");
        }
    }
}
