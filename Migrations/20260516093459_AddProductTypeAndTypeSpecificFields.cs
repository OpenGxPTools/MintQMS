using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTypeAndTypeSpecificFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Strength",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "DeviceTypeCategory",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InciName",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelVersion",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentationForm",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                table: "ReleaseRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawMaterialTypeCategory",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardScientificName",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceTypeCategory",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InciName",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelVersion",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentationForm",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                table: "Product",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawMaterialTypeCategory",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardScientificName",
                table: "Product",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceTypeCategory",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "InciName",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "PresentationForm",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "RawMaterialTypeCategory",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "StandardScientificName",
                table: "ReleaseRecords");

            migrationBuilder.DropColumn(
                name: "DeviceTypeCategory",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "InciName",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "PresentationForm",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "RawMaterialTypeCategory",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "StandardScientificName",
                table: "Product");

            migrationBuilder.AlterColumn<string>(
                name: "Strength",
                table: "ReleaseRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
