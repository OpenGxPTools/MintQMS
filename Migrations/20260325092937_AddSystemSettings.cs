using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettingsAuditLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingsAuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganisationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OrganisationAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    LogoPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DateFormat = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DefaultPageSize = table.Column<int>(type: "INTEGER", nullable: false),
                    LcExpiryWarningDays = table.Column<int>(type: "INTEGER", nullable: false),
                    MaintenanceDueWarningDays = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainingDueWarningDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultCapaResolutionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultDeviationResolutionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultComplaintResolutionDays = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettingsAuditLog");

            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
