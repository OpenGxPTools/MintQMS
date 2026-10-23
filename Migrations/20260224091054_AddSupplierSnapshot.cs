using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierSnapshot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    SupplierCode = table.Column<string>(type: "TEXT", nullable: false),
                    LegalName = table.Column<string>(type: "TEXT", nullable: false),
                    TradingName = table.Column<string>(type: "TEXT", nullable: true),
                    LegalId = table.Column<string>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    SupportedSites = table.Column<string>(type: "TEXT", nullable: true),
                    Street = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    PostCode = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    ContactName = table.Column<string>(type: "TEXT", nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ContactPhone = table.Column<string>(type: "TEXT", nullable: true),
                    SupplierType = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedItems = table.Column<string>(type: "TEXT", nullable: true),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskJustification = table.Column<string>(type: "TEXT", nullable: true),
                    AssessedBy = table.Column<string>(type: "TEXT", nullable: true),
                    AssessedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalComments = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    QualificationRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QualifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    QualifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QualificationBasis = table.Column<int>(type: "INTEGER", nullable: true),
                    QualificationJustification = table.Column<string>(type: "TEXT", nullable: true),
                    QualificationReferenceDoc = table.Column<string>(type: "TEXT", nullable: true),
                    QualificationComments = table.Column<string>(type: "TEXT", nullable: true),
                    SuspendedBy = table.Column<string>(type: "TEXT", nullable: true),
                    SuspendedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SuspensionReason = table.Column<string>(type: "TEXT", nullable: true),
                    CancelledBy = table.Column<string>(type: "TEXT", nullable: true),
                    CancelledOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewTrigger = table.Column<int>(type: "INTEGER", nullable: true),
                    ReasonForReview = table.Column<string>(type: "TEXT", nullable: true),
                    LinkedQualityRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkedQualityRecordCode = table.Column<string>(type: "TEXT", nullable: true),
                    LinkedQualityRecordTitle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierSnapshot_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierSnapshotDocument",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UploadedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSnapshotDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierSnapshotDocument_SupplierSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "SupplierSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierSnapshotQualityRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordType = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CapaId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: true),
                    RiskId = table.Column<int>(type: "INTEGER", nullable: true),
                    AuditId = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordCode = table.Column<string>(type: "TEXT", nullable: true),
                    RecordTitle = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    LinkedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LinkedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSnapshotQualityRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierSnapshotQualityRecord_SupplierSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "SupplierSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSnapshot_SupplierId",
                table: "SupplierSnapshot",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSnapshotDocument_SnapshotId",
                table: "SupplierSnapshotDocument",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSnapshotQualityRecord_SnapshotId",
                table: "SupplierSnapshotQualityRecord",
                column: "SnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierSnapshotDocument");

            migrationBuilder.DropTable(
                name: "SupplierSnapshotQualityRecord");

            migrationBuilder.DropTable(
                name: "SupplierSnapshot");
        }
    }
}
