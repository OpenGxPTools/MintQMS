using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class SiteLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowUserLanguageChoice",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SiteLanguage",
                table: "SystemSettings",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");   // existing sites keep English
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowUserLanguageChoice",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "SiteLanguage",
                table: "SystemSettings");
        }
    }
}
