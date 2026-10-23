using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRisksModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Risk",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RiskId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Probability = table.Column<int>(type: "INTEGER", nullable: false),
                    Detectability = table.Column<int>(type: "INTEGER", nullable: false),
                    InitialRPN = table.Column<int>(type: "INTEGER", nullable: false),
                    IdentifiedBy = table.Column<string>(type: "TEXT", nullable: false),
                    IdentifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RiskOwner = table.Column<string>(type: "TEXT", nullable: true),
                    AssessedBy = table.Column<string>(type: "TEXT", nullable: true),
                    AssessedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MitigationStrategy = table.Column<string>(type: "TEXT", nullable: true),
                    TargetCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MitigatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    MitigatedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResidualSeverity = table.Column<int>(type: "INTEGER", nullable: true),
                    ResidualProbability = table.Column<int>(type: "INTEGER", nullable: true),
                    ResidualDetectability = table.Column<int>(type: "INTEGER", nullable: true),
                    ResidualRPN = table.Column<int>(type: "INTEGER", nullable: true),
                    RiskAcceptable = table.Column<bool>(type: "INTEGER", nullable: true),
                    Justification = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MonitoringPlan = table.Column<string>(type: "TEXT", nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExportFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Risk", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Risk_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Risk_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Risk_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Risk_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RiskAttachment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RiskId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_RiskAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskAttachment_Risk_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Risk_AssetId",
                table: "Risk",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Risk_MaterialId",
                table: "Risk",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Risk_ProcessId",
                table: "Risk",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Risk_ProductId",
                table: "Risk",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAttachment_RiskId",
                table: "RiskAttachment",
                column: "RiskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskAttachment");

            migrationBuilder.DropTable(
                name: "Risk");
        }
    }
}
