using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditIdToCapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuditId",
                table: "Capa",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capa_AuditId",
                table: "Capa",
                column: "AuditId");

            migrationBuilder.AddForeignKey(
                name: "FK_Capa_Audit_AuditId",
                table: "Capa",
                column: "AuditId",
                principalTable: "Audit",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Capa_Audit_AuditId",
                table: "Capa");

            migrationBuilder.DropIndex(
                name: "IX_Capa_AuditId",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "AuditId",
                table: "Capa");
        }
    }
}
