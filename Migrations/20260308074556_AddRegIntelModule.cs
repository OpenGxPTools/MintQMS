using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRegIntelModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RIJurisdictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Authority = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AuthorityUrl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PortalUrl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIJurisdictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RIApplicationTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JurisdictionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClockDays = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIApplicationTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIApplicationTypes_RIJurisdictions_JurisdictionId",
                        column: x => x.JurisdictionId,
                        principalTable: "RIJurisdictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RINumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    JurisdictionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActiveSubstance = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Indication = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Strength = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AuthorityReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SubmissionMethod = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOrphanDesignation = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPriorityReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBreakthroughTherapy = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPRIME = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsConditionalApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    StrategyStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreSubMeetingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DossierLockDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgementDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Day0Date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TargetDecisionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualDecisionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRenewalDue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AppealDeadline = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApplicationFee = table.Column<decimal>(type: "TEXT", nullable: true),
                    FeeCurrency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    FeePaidDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FeeWaiverApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    FeeWaiverReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    InternalRALead = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ExternalConsultant = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    AuthorityRapporteur = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    AuthorityContactEmail = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    ManufacturingSupplierId = table.Column<int>(type: "INTEGER", nullable: true),
                    StrategyNotes = table.Column<string>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true),
                    WithdrawalReason = table.Column<string>(type: "TEXT", nullable: true),
                    ParentApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryApplications_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegulatoryApplications_RIApplicationTypes_ApplicationTypeId",
                        column: x => x.ApplicationTypeId,
                        principalTable: "RIApplicationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegulatoryApplications_RIJurisdictions_JurisdictionId",
                        column: x => x.JurisdictionId,
                        principalTable: "RIJurisdictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegulatoryApplications_RegulatoryApplications_ParentApplicationId",
                        column: x => x.ParentApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RIAccessPathwayRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegulatoryApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccessNumber = table.Column<string>(type: "TEXT", maxLength: 25, nullable: false),
                    PathwayType = table.Column<int>(type: "INTEGER", nullable: false),
                    PatientIdentifier = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PrescriberId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PrescriberName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TGAApprovalLetterNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    NextSixMonthlyReportDue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AEReportingRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClinicalJustification = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIAccessPathwayRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIAccessPathwayRecords_RegulatoryApplications_RegulatoryApplicationId",
                        column: x => x.RegulatoryApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RIAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegulatoryApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FieldChanged = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIAuditLogs_RegulatoryApplications_RegulatoryApplicationId",
                        column: x => x.RegulatoryApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RIConditionsOfApproval",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegulatoryApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ConditionType = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletionNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LinkedCAPAId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIConditionsOfApproval", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIConditionsOfApproval_RegulatoryApplications_RegulatoryApplicationId",
                        column: x => x.RegulatoryApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RIDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegulatoryApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIDocuments_RegulatoryApplications_RegulatoryApplicationId",
                        column: x => x.RegulatoryApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RIMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegulatoryApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    MilestoneName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsClockStop = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClockRestartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIMilestones_RegulatoryApplications_RegulatoryApplicationId",
                        column: x => x.RegulatoryApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RIVariations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    VariationNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    VariationDescription = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    VariationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RIVariations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RIVariations_RegulatoryApplications_ParentApplicationId",
                        column: x => x.ParentApplicationId,
                        principalTable: "RegulatoryApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RISixMonthlySupplyReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccessPathwayRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    QuantitySupplied = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SubmittedToAuthorityDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AuthorityReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RISixMonthlySupplyReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RISixMonthlySupplyReports_RIAccessPathwayRecords_AccessPathwayRecordId",
                        column: x => x.AccessPathwayRecordId,
                        principalTable: "RIAccessPathwayRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryApplications_ApplicationTypeId",
                table: "RegulatoryApplications",
                column: "ApplicationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryApplications_JurisdictionId",
                table: "RegulatoryApplications",
                column: "JurisdictionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryApplications_ParentApplicationId",
                table: "RegulatoryApplications",
                column: "ParentApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryApplications_ProductId",
                table: "RegulatoryApplications",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RIAccessPathwayRecords_RegulatoryApplicationId",
                table: "RIAccessPathwayRecords",
                column: "RegulatoryApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RIApplicationTypes_JurisdictionId",
                table: "RIApplicationTypes",
                column: "JurisdictionId");

            migrationBuilder.CreateIndex(
                name: "IX_RIAuditLogs_RegulatoryApplicationId",
                table: "RIAuditLogs",
                column: "RegulatoryApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RIConditionsOfApproval_RegulatoryApplicationId",
                table: "RIConditionsOfApproval",
                column: "RegulatoryApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RIDocuments_RegulatoryApplicationId",
                table: "RIDocuments",
                column: "RegulatoryApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RIMilestones_RegulatoryApplicationId",
                table: "RIMilestones",
                column: "RegulatoryApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RISixMonthlySupplyReports_AccessPathwayRecordId",
                table: "RISixMonthlySupplyReports",
                column: "AccessPathwayRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_RIVariations_ParentApplicationId",
                table: "RIVariations",
                column: "ParentApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RIAuditLogs");

            migrationBuilder.DropTable(
                name: "RIConditionsOfApproval");

            migrationBuilder.DropTable(
                name: "RIDocuments");

            migrationBuilder.DropTable(
                name: "RIMilestones");

            migrationBuilder.DropTable(
                name: "RISixMonthlySupplyReports");

            migrationBuilder.DropTable(
                name: "RIVariations");

            migrationBuilder.DropTable(
                name: "RIAccessPathwayRecords");

            migrationBuilder.DropTable(
                name: "RegulatoryApplications");

            migrationBuilder.DropTable(
                name: "RIApplicationTypes");

            migrationBuilder.DropTable(
                name: "RIJurisdictions");
        }
    }
}
