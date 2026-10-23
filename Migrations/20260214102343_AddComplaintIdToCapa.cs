using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintIdToCapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComplaintId",
                table: "Capa",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capa_ComplaintId",
                table: "Capa",
                column: "ComplaintId");

            migrationBuilder.AddForeignKey(
                name: "FK_Capa_Complaint_ComplaintId",
                table: "Capa",
                column: "ComplaintId",
                principalTable: "Complaint",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Capa_Complaint_ComplaintId",
                table: "Capa");

            migrationBuilder.DropIndex(
                name: "IX_Capa_ComplaintId",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "ComplaintId",
                table: "Capa");
        }
    }
}
