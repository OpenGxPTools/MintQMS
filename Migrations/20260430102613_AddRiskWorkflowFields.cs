using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptedBy",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedOn",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssessmentNotes",
                table: "Risk",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewConclusion",
                table: "Risk",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedBy",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "AcceptedOn",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "AssessmentNotes",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "ReviewConclusion",
                table: "Risk");
        }
    }
}
