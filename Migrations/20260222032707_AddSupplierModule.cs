using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Supplier",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierCode = table.Column<string>(type: "TEXT", nullable: false),
                    LegalName = table.Column<string>(type: "TEXT", nullable: false),
                    TradingName = table.Column<string>(type: "TEXT", nullable: true),
                    LegalId = table.Column<string>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    Street = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    PostCode = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    ContactName = table.Column<string>(type: "TEXT", nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ContactPhone = table.Column<string>(type: "TEXT", nullable: true),
                    SupplierType = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskJustification = table.Column<string>(type: "TEXT", nullable: true),
                    SupportedSites = table.Column<string>(type: "TEXT", nullable: true),
                    QualificationRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    TestingRequirement = table.Column<int>(type: "INTEGER", nullable: false),
                    QualificationJustification = table.Column<string>(type: "TEXT", nullable: true),
                    QualificationBasis = table.Column<int>(type: "INTEGER", nullable: true),
                    QualificationReferenceDoc = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastReviewDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewTrigger = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AssessedBy = table.Column<string>(type: "TEXT", nullable: true),
                    AssessedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QualifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    QualifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SuspendedBy = table.Column<string>(type: "TEXT", nullable: true),
                    SuspendedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SuspensionReason = table.Column<string>(type: "TEXT", nullable: true),
                    CancelledBy = table.Column<string>(type: "TEXT", nullable: true),
                    CancelledOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ExportFilePath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supplier", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDocument",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReminderDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    UploadedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierDocument_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierMaterial",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierMaterial", x => new { x.SupplierId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_SupplierMaterial_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierMaterial_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProduct",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProduct", x => new { x.SupplierId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_SupplierProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierProduct_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQualityRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordType = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CapaId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: true),
                    RiskId = table.Column<int>(type: "INTEGER", nullable: true),
                    AuditId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    LinkedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LinkedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierQualityRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierQualityRecord_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReview",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewTrigger = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewSummary = table.Column<string>(type: "TEXT", nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", nullable: true),
                    QualificationJustification = table.Column<string>(type: "TEXT", nullable: true),
                    QualificationBasis = table.Column<int>(type: "INTEGER", nullable: true),
                    QualificationReferenceDoc = table.Column<string>(type: "TEXT", nullable: true),
                    SuspensionReason = table.Column<string>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReview", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReview_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierService",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierService", x => new { x.SupplierId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_SupplierService_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierService_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocument_SupplierId",
                table: "SupplierDocument",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierMaterial_MaterialId",
                table: "SupplierMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProduct_ProductId",
                table: "SupplierProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQualityRecord_SupplierId",
                table: "SupplierQualityRecord",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReview_SupplierId",
                table: "SupplierReview",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierService_ServiceId",
                table: "SupplierService",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierDocument");

            migrationBuilder.DropTable(
                name: "SupplierMaterial");

            migrationBuilder.DropTable(
                name: "SupplierProduct");

            migrationBuilder.DropTable(
                name: "SupplierQualityRecord");

            migrationBuilder.DropTable(
                name: "SupplierReview");

            migrationBuilder.DropTable(
                name: "SupplierService");

            migrationBuilder.DropTable(
                name: "Supplier");
        }
    }
}
