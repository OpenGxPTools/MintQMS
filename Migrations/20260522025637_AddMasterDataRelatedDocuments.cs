using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataRelatedDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetLinkedDocument",
                columns: table => new
                {
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppDocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedVersion = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetLinkedDocument", x => new { x.AssetId, x.AppDocumentId });
                    table.ForeignKey(
                        name: "FK_AssetLinkedDocument_AppDocument_AppDocumentId",
                        column: x => x.AppDocumentId,
                        principalTable: "AppDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetLinkedDocument_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLinkedDocument",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppDocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedVersion = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLinkedDocument", x => new { x.CustomerId, x.AppDocumentId });
                    table.ForeignKey(
                        name: "FK_CustomerLinkedDocument_AppDocument_AppDocumentId",
                        column: x => x.AppDocumentId,
                        principalTable: "AppDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerLinkedDocument_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialLinkedDocument",
                columns: table => new
                {
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppDocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedVersion = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialLinkedDocument", x => new { x.MaterialId, x.AppDocumentId });
                    table.ForeignKey(
                        name: "FK_MaterialLinkedDocument_AppDocument_AppDocumentId",
                        column: x => x.AppDocumentId,
                        principalTable: "AppDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialLinkedDocument_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessLinkedDocument",
                columns: table => new
                {
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppDocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedVersion = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessLinkedDocument", x => new { x.ProcessId, x.AppDocumentId });
                    table.ForeignKey(
                        name: "FK_ProcessLinkedDocument_AppDocument_AppDocumentId",
                        column: x => x.AppDocumentId,
                        principalTable: "AppDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessLinkedDocument_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductLinkedDocument",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppDocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedVersion = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductLinkedDocument", x => new { x.ProductId, x.AppDocumentId });
                    table.ForeignKey(
                        name: "FK_ProductLinkedDocument_AppDocument_AppDocumentId",
                        column: x => x.AppDocumentId,
                        principalTable: "AppDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductLinkedDocument_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLinkedDocument",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppDocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedVersion = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLinkedDocument", x => new { x.ServiceId, x.AppDocumentId });
                    table.ForeignKey(
                        name: "FK_ServiceLinkedDocument_AppDocument_AppDocumentId",
                        column: x => x.AppDocumentId,
                        principalTable: "AppDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceLinkedDocument_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetLinkedDocument_AppDocumentId",
                table: "AssetLinkedDocument",
                column: "AppDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLinkedDocument_AppDocumentId",
                table: "CustomerLinkedDocument",
                column: "AppDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialLinkedDocument_AppDocumentId",
                table: "MaterialLinkedDocument",
                column: "AppDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLinkedDocument_AppDocumentId",
                table: "ProcessLinkedDocument",
                column: "AppDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductLinkedDocument_AppDocumentId",
                table: "ProductLinkedDocument",
                column: "AppDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLinkedDocument_AppDocumentId",
                table: "ServiceLinkedDocument",
                column: "AppDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetLinkedDocument");

            migrationBuilder.DropTable(
                name: "CustomerLinkedDocument");

            migrationBuilder.DropTable(
                name: "MaterialLinkedDocument");

            migrationBuilder.DropTable(
                name: "ProcessLinkedDocument");

            migrationBuilder.DropTable(
                name: "ProductLinkedDocument");

            migrationBuilder.DropTable(
                name: "ServiceLinkedDocument");
        }
    }
}
