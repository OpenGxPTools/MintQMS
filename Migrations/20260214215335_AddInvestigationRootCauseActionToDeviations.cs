using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestigationRootCauseActionToDeviations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionBy",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActionOn",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectiveAction",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvestigatedBy",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvestigatedOn",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Investigation",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCauseBy",
                table: "Deviation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RootCauseOn",
                table: "Deviation",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionBy",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "ActionOn",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "CorrectiveAction",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "InvestigatedBy",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "InvestigatedOn",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "Investigation",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RootCauseBy",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RootCauseOn",
                table: "Deviation");
        }
    }
}
