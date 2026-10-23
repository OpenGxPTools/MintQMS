using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedAEIdToComplaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedAEId",
                table: "Complaint",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_LinkedAEId",
                table: "Complaint",
                column: "LinkedAEId");

            migrationBuilder.AddForeignKey(
                name: "FK_Complaint_AdverseEvent_LinkedAEId",
                table: "Complaint",
                column: "LinkedAEId",
                principalTable: "AdverseEvent",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Complaint_AdverseEvent_LinkedAEId",
                table: "Complaint");

            migrationBuilder.DropIndex(
                name: "IX_Complaint_LinkedAEId",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "LinkedAEId",
                table: "Complaint");
        }
    }
}
