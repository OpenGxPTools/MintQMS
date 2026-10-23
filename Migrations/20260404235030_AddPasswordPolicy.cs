using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PasswordExpiryDays",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PasswordHistoryCount",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedOn",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PasswordHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordHistory", x => x.Id);
                });

            // Seed: stamp all existing users with PasswordChangedOn = now so nobody
            // is immediately forced to change their password after the migration runs.
            migrationBuilder.Sql(
                "UPDATE AspNetUsers SET PasswordChangedOn = datetime('now') WHERE PasswordChangedOn IS NULL;");

            // Seed: set sensible GxP defaults on the existing SystemSettings row (if any).
            migrationBuilder.Sql(
                "UPDATE SystemSettings SET PasswordExpiryDays = 90, PasswordHistoryCount = 5 WHERE Id = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordHistory");

            migrationBuilder.DropColumn(
                name: "PasswordExpiryDays",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PasswordHistoryCount",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PasswordChangedOn",
                table: "AspNetUsers");
        }
    }
}
