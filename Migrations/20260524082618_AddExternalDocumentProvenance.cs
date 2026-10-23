using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalDocumentProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalApprovedOn",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalApproverName",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAuthorName",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalAuthoredOn",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSource",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalVersion",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternal",
                table: "AppDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalApprovedOn",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "ExternalApproverName",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "ExternalAuthorName",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "ExternalAuthoredOn",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "ExternalSource",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "ExternalVersion",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "IsExternal",
                table: "AppDocument");
        }
    }
}
