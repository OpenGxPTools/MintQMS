using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeAssessmentAndActionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChangeType",
                table: "Change",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DetailedActionPlanRequired",
                table: "Change",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Nature",
                table: "Change",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiskClassification",
                table: "Change",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChangeActionPlanItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceReference = table.Column<string>(type: "TEXT", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeActionPlanItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeActionPlanItem_Change_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Change",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeActionPlanItem_ChangeId",
                table: "ChangeActionPlanItem",
                column: "ChangeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeActionPlanItem");

            migrationBuilder.DropColumn(
                name: "ChangeType",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "DetailedActionPlanRequired",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "Nature",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "RiskClassification",
                table: "Change");
        }
    }
}
