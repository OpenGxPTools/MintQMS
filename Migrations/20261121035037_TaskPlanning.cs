using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <summary>Task assignment planning (1.2.0): a person can be planned for a step still ahead (StartedOn null until
    /// the record reaches it), due a number of days after it starts (DueDays).</summary>
    public partial class TaskPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DueDays",
                table: "TaskAssignments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedOn",
                table: "TaskAssignments",
                type: "TEXT",
                nullable: true);

            // Every assignment made before planning existed was for the step the record was at: it had started.
            migrationBuilder.Sql("UPDATE \"TaskAssignments\" SET \"StartedOn\" = \"AssignedOn\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDays",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "StartedOn",
                table: "TaskAssignments");
        }
    }
}
