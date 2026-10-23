using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddLicCertModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LCJurisdictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JurisdictionCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IssuingBodyUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PortalUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LCJurisdictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LCLicenceTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultRenewalLeadTimeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresExpiryDate = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApplicableHolderType = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuingBody = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApplicableJurisdiction = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LCLicenceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicenceCertificationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LCNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HolderType = table.Column<int>(type: "INTEGER", nullable: false),
                    HolderName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkedSiteAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LinkedSiteCountry = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    LicenceTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuingBody = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IssuingBodyUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IssuingBodyPortal = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    LicenceNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Scope = table.Column<string>(type: "TEXT", nullable: true),
                    ApplicationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRenewalDue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RenewalLeadTimeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    GmcPathway = table.Column<int>(type: "INTEGER", nullable: true),
                    SiteCountry = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ManufacturingStepsScope = table.Column<string>(type: "TEXT", nullable: true),
                    LastInspectionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InspectionAuthority = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RFIDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RFIResponseDueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RFIResponseSubmittedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LetterOfAccessAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CertificationBody = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CertificationBodyAccreditation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StandardVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CertificateScope = table.Column<string>(type: "TEXT", nullable: true),
                    Stage1AuditDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Stage2AuditDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextSurveillanceAuditDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RecertificationDueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InternalResponsiblePersonId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExternalConsultant = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AuthorityContactEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AuthorityPortalUsername = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApplicationFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FeeCurrency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FeePaidDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FeeReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    JustificationReason = table.Column<string>(type: "TEXT", nullable: true),
                    RegulatoryActionNotes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenceCertificationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenceCertificationRecords_LCLicenceTypes_LicenceTypeId",
                        column: x => x.LicenceTypeId,
                        principalTable: "LCLicenceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LicenceCertificationRecords_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LCAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicenceCertificationRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LCAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LCAuditLogs_LicenceCertificationRecords_LicenceCertificationRecordId",
                        column: x => x.LicenceCertificationRecordId,
                        principalTable: "LicenceCertificationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LCConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicenceCertificationRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionDescription = table.Column<string>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedCAPAId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletionNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LCConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LCConditions_LicenceCertificationRecords_LicenceCertificationRecordId",
                        column: x => x.LicenceCertificationRecordId,
                        principalTable: "LicenceCertificationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LCDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicenceCertificationRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegulatoryApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedSince = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LCDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LCDependencies_LicenceCertificationRecords_LicenceCertificationRecordId",
                        column: x => x.LicenceCertificationRecordId,
                        principalTable: "LicenceCertificationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LCDependencies_RegulatoryApplications_RegulatoryApplicationId",
                        column: x => x.RegulatoryApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LCDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicenceCertificationRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedById = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LCDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LCDocuments_LicenceCertificationRecords_LicenceCertificationRecordId",
                        column: x => x.LicenceCertificationRecordId,
                        principalTable: "LicenceCertificationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LCAuditLogs_LicenceCertificationRecordId",
                table: "LCAuditLogs",
                column: "LicenceCertificationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_LCConditions_LicenceCertificationRecordId",
                table: "LCConditions",
                column: "LicenceCertificationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_LCDependencies_LicenceCertificationRecordId",
                table: "LCDependencies",
                column: "LicenceCertificationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_LCDependencies_RegulatoryApplicationId",
                table: "LCDependencies",
                column: "RegulatoryApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LCDocuments_LicenceCertificationRecordId",
                table: "LCDocuments",
                column: "LicenceCertificationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenceCertificationRecords_LicenceTypeId",
                table: "LicenceCertificationRecords",
                column: "LicenceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenceCertificationRecords_SupplierId",
                table: "LicenceCertificationRecords",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LCAuditLogs");

            migrationBuilder.DropTable(
                name: "LCConditions");

            migrationBuilder.DropTable(
                name: "LCDependencies");

            migrationBuilder.DropTable(
                name: "LCDocuments");

            migrationBuilder.DropTable(
                name: "LCJurisdictions");

            migrationBuilder.DropTable(
                name: "LicenceCertificationRecords");

            migrationBuilder.DropTable(
                name: "LCLicenceTypes");
        }
    }
}
