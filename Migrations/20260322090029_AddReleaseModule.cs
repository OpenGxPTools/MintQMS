using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecordNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReleaseType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BrandName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    InnName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Strength = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ArtgReference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BatchSize = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PackSize = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ManufacturingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RetestDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ManufacturingSite = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ManufacturingSiteGmpRef = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TestingLaboratory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LabAccreditationRef = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReleasingCompany = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReleasingCompanyLicenceRef = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    FinalReleaseParty = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TechnicalAgreementRef = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SpecificationReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StorageConditions = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    DistributionConditions = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OpenDeviationsNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LimitationsNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    HoldReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    HoldDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HoldBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    APCertificationStatement = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CertificationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CertifiedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    APFullNameQualification = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ReleaseRegisterRef = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RejectionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IPAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseAuditEntries_ReleaseRecords_ReleaseRecordId",
                        column: x => x.ReleaseRecordId,
                        principalTable: "ReleaseRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseCertificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    CertificateNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CertificateType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    PdfFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    SupersededReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SupersededById = table.Column<int>(type: "INTEGER", nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IssuedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    APFullNameQualification = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseCertificates_ReleaseRecords_ReleaseRecordId",
                        column: x => x.ReleaseRecordId,
                        principalTable: "ReleaseRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemText = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsBlocking = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseChecklistItems_ReleaseRecords_ReleaseRecordId",
                        column: x => x.ReleaseRecordId,
                        principalTable: "ReleaseRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseCrossReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModuleType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LinkedRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkedRecordNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCrossReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseCrossReferences_ReleaseRecords_ReleaseRecordId",
                        column: x => x.ReleaseRecordId,
                        principalTable: "ReleaseRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DocumentCategory = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseDocuments_ReleaseRecords_ReleaseRecordId",
                        column: x => x.ReleaseRecordId,
                        principalTable: "ReleaseRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseTestResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TestParameter = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TestMethod = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AcceptanceCriterion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EnteredBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EnteredDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseTestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseTestResults_ReleaseRecords_ReleaseRecordId",
                        column: x => x.ReleaseRecordId,
                        principalTable: "ReleaseRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseAuditEntries_ReleaseRecordId",
                table: "ReleaseAuditEntries",
                column: "ReleaseRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCertificates_ReleaseRecordId",
                table: "ReleaseCertificates",
                column: "ReleaseRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseChecklistItems_ReleaseRecordId",
                table: "ReleaseChecklistItems",
                column: "ReleaseRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCrossReferences_ReleaseRecordId",
                table: "ReleaseCrossReferences",
                column: "ReleaseRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseDocuments_ReleaseRecordId",
                table: "ReleaseDocuments",
                column: "ReleaseRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTestResults_ReleaseRecordId",
                table: "ReleaseTestResults",
                column: "ReleaseRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReleaseAuditEntries");

            migrationBuilder.DropTable(
                name: "ReleaseCertificates");

            migrationBuilder.DropTable(
                name: "ReleaseChecklistItems");

            migrationBuilder.DropTable(
                name: "ReleaseCrossReferences");

            migrationBuilder.DropTable(
                name: "ReleaseDocuments");

            migrationBuilder.DropTable(
                name: "ReleaseTestResults");

            migrationBuilder.DropTable(
                name: "ReleaseRecords");
        }
    }
}
