using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierIdToQualityModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Risk",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Deviation",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Complaint",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Change",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Capa",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Audit",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Risk_SupplierId",
                table: "Risk",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Deviation_SupplierId",
                table: "Deviation",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_SupplierId",
                table: "Complaint",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Change_SupplierId",
                table: "Change",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Capa_SupplierId",
                table: "Capa",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_SupplierId",
                table: "Audit",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Audit_Supplier_SupplierId",
                table: "Audit",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Capa_Supplier_SupplierId",
                table: "Capa",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Change_Supplier_SupplierId",
                table: "Change",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Complaint_Supplier_SupplierId",
                table: "Complaint",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Deviation_Supplier_SupplierId",
                table: "Deviation",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Risk_Supplier_SupplierId",
                table: "Risk",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Audit_Supplier_SupplierId",
                table: "Audit");

            migrationBuilder.DropForeignKey(
                name: "FK_Capa_Supplier_SupplierId",
                table: "Capa");

            migrationBuilder.DropForeignKey(
                name: "FK_Change_Supplier_SupplierId",
                table: "Change");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaint_Supplier_SupplierId",
                table: "Complaint");

            migrationBuilder.DropForeignKey(
                name: "FK_Deviation_Supplier_SupplierId",
                table: "Deviation");

            migrationBuilder.DropForeignKey(
                name: "FK_Risk_Supplier_SupplierId",
                table: "Risk");

            migrationBuilder.DropIndex(
                name: "IX_Risk_SupplierId",
                table: "Risk");

            migrationBuilder.DropIndex(
                name: "IX_Deviation_SupplierId",
                table: "Deviation");

            migrationBuilder.DropIndex(
                name: "IX_Complaint_SupplierId",
                table: "Complaint");

            migrationBuilder.DropIndex(
                name: "IX_Change_SupplierId",
                table: "Change");

            migrationBuilder.DropIndex(
                name: "IX_Capa_SupplierId",
                table: "Capa");

            migrationBuilder.DropIndex(
                name: "IX_Audit_SupplierId",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Risk");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Change");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Capa");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Audit");
        }
    }
}
