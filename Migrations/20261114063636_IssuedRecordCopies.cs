using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class IssuedRecordCopies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedCopiesSince",
                table: "SystemSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IssuedRecordCopies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Html = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SupersededOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SupersededReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedRecordCopies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedRecordCopies_Kind_SourceId",
                table: "IssuedRecordCopies",
                columns: new[] { "Kind", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssuedRecordCopies");

            migrationBuilder.DropColumn(
                name: "IssuedCopiesSince",
                table: "SystemSettings");
        }
    }
}
