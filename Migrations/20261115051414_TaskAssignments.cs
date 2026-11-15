using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class TaskAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Module = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedToUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AssignedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    EndedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndReason = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    EndedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AssignedToUserId_IsCurrent",
                table: "TaskAssignments",
                columns: new[] { "AssignedToUserId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_Module_RecordId_IsCurrent",
                table: "TaskAssignments",
                columns: new[] { "Module", "RecordId", "IsCurrent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskAssignments");
        }
    }
}
