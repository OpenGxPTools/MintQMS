using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFileHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "SupplierDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ServiceDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "RiskAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "RIDocuments",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ReleaseDocuments",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ProductDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ProcessDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "MaterialDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "MaintenanceAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "LCDocuments",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "DeviationAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "CustomerDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ComplaintAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ChangeAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "CapaAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "AuditAttachment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "AssetDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "AEAttachment",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "SupplierDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ServiceDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "RiskAttachment");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "RIDocuments");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ReleaseDocuments");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ProductDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ProcessDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "MaterialDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "MaintenanceAttachment");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "LCDocuments");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "DeviationAttachment");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "CustomerDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ComplaintAttachment");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ChangeAttachment");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "CapaAttachment");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "AuditAttachment");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "AssetDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "AEAttachment");
        }
    }
}
