using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class FourEyesSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnforceFourEyes",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                // On for existing installations too (owner decision 2026-11-15); an Administrator may turn it off.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnforceFourEyes",
                table: "SystemSettings");
        }
    }
}
