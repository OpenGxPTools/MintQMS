using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class LinkCustomerToComplaints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "Complaint",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Complaint",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_CustomerId",
                table: "Complaint",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Complaint_Customer_CustomerId",
                table: "Complaint",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Complaint_Customer_CustomerId",
                table: "Complaint");

            migrationBuilder.DropIndex(
                name: "IX_Complaint_CustomerId",
                table: "Complaint");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Complaint");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "Complaint",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
