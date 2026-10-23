using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRootCauseFromComplaints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RootCauseBy",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RootCauseOn",
                table: "Complaint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCauseBy",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RootCauseOn",
                table: "Complaint",
                type: "TEXT",
                nullable: true);
        }
    }
}
