using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRegulatoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtgReference",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandName",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DosageForm",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InnName",
                table: "Product",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductStrengths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStrengths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductStrengths_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductStrengths_ProductId",
                table: "ProductStrengths",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductStrengths");

            migrationBuilder.DropColumn(
                name: "ArtgReference",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "BrandName",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "DosageForm",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "InnName",
                table: "Product");
        }
    }
}
