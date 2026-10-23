using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPVCrossModuleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedAdverseEventId",
                table: "Capa",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedCapaId",
                table: "AdverseEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedComplaintId",
                table: "AdverseEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capa_LinkedAdverseEventId",
                table: "Capa",
                column: "LinkedAdverseEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AdverseEvent_LinkedCapaId",
                table: "AdverseEvent",
                column: "LinkedCapaId");

            migrationBuilder.CreateIndex(
                name: "IX_AdverseEvent_LinkedComplaintId",
                table: "AdverseEvent",
                column: "LinkedComplaintId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdverseEvent_Capa_LinkedCapaId",
                table: "AdverseEvent",
                column: "LinkedCapaId",
                principalTable: "Capa",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AdverseEvent_Complaint_LinkedComplaintId",
                table: "AdverseEvent",
                column: "LinkedComplaintId",
                principalTable: "Complaint",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Capa_AdverseEvent_LinkedAdverseEventId",
                table: "Capa",
                column: "LinkedAdverseEventId",
                principalTable: "AdverseEvent",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdverseEvent_Capa_LinkedCapaId",
                table: "AdverseEvent");

            migrationBuilder.DropForeignKey(
                name: "FK_AdverseEvent_Complaint_LinkedComplaintId",
                table: "AdverseEvent");

            migrationBuilder.DropForeignKey(
                name: "FK_Capa_AdverseEvent_LinkedAdverseEventId",
                table: "Capa");

            migrationBuilder.DropIndex(
                name: "IX_Capa_LinkedAdverseEventId",
                table: "Capa");

            migrationBuilder.DropIndex(
                name: "IX_AdverseEvent_LinkedCapaId",
                table: "AdverseEvent");

            migrationBuilder.DropIndex(
                name: "IX_AdverseEvent_LinkedComplaintId",
                table: "AdverseEvent");

            migrationBuilder.DropColumn(
                name: "LinkedAdverseEventId",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "LinkedCapaId",
                table: "AdverseEvent");

            migrationBuilder.DropColumn(
                name: "LinkedComplaintId",
                table: "AdverseEvent");
        }
    }
}
