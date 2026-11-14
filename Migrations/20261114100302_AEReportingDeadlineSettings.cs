using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AEReportingDeadlineSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FatalAEReportingDays",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7);   // the deadline MintQMS always used: an existing site keeps it

            migrationBuilder.AddColumn<int>(
                name: "SeriousAEReportingDays",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 15);  // the deadline MintQMS always used: an existing site keeps it
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FatalAEReportingDays",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "SeriousAEReportingDays",
                table: "SystemSettings");
        }
    }
}
