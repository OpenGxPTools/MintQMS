using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSpecLinesAndReleaseLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedProductId",
                table: "ReleaseRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductSpecLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TestParameter = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AcceptanceCriterion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Comments = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecLines_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecLines_ProductId",
                table: "ProductSpecLines",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductSpecLines");

            migrationBuilder.DropColumn(
                name: "LinkedProductId",
                table: "ReleaseRecords");
        }
    }
}
