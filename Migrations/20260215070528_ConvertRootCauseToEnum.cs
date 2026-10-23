using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRootCauseToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RootCauseCategory",
                table: "Deviation",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCauseBy",
                table: "Complaint",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RootCauseCategory",
                table: "Complaint",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RootCauseOn",
                table: "Complaint",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RootCauseCategory",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "RootCauseBy",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RootCauseCategory",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "RootCauseOn",
                table: "Complaint");
        }
    }
}
