using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmVigilanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdverseEvent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AENumber = table.Column<string>(type: "TEXT", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Day0Date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TGADeadlineDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReportType = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportSource = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentAEId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSerious = table.Column<bool>(type: "INTEGER", nullable: false),
                    SeriousnessCriteria = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFatalOrLifeThreatening = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReportable = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    AEDescription = table.Column<string>(type: "TEXT", nullable: true),
                    MedDRAPT = table.Column<string>(type: "TEXT", nullable: true),
                    MedDRASOC = table.Column<string>(type: "TEXT", nullable: true),
                    AEStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AEEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: true),
                    Causality = table.Column<int>(type: "INTEGER", nullable: true),
                    Severity = table.Column<int>(type: "INTEGER", nullable: true),
                    Expectedness = table.Column<int>(type: "INTEGER", nullable: true),
                    MedicalNarrative = table.Column<string>(type: "TEXT", nullable: true),
                    MedicalEvaluator = table.Column<string>(type: "TEXT", nullable: true),
                    EvaluationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignalDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdverseEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdverseEvent_AdverseEvent_ParentAEId",
                        column: x => x.ParentAEId,
                        principalTable: "AdverseEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PVLiteratureSearch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SearchDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SearchedBy = table.Column<string>(type: "TEXT", nullable: false),
                    DatabasesSearched = table.Column<string>(type: "TEXT", nullable: true),
                    SearchTerms = table.Column<string>(type: "TEXT", nullable: true),
                    DateRangeFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateRangeTo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResultsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RelevantResults = table.Column<int>(type: "INTEGER", nullable: false),
                    AEsIdentified = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVLiteratureSearch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PVPeriodicReport",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TGAReferenceNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ExposureData = table.Column<string>(type: "TEXT", nullable: true),
                    BenefitRiskConclusion = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVPeriodicReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PVPeriodicReport_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AEAttachment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdverseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AEAttachment_AdverseEvent_AdverseEventId",
                        column: x => x.AdverseEventId,
                        principalTable: "AdverseEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AEAuditLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdverseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    PerformedBy = table.Column<string>(type: "TEXT", nullable: false),
                    PerformedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AEAuditLog_AdverseEvent_AdverseEventId",
                        column: x => x.AdverseEventId,
                        principalTable: "AdverseEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AEPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdverseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    PatientIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Age = table.Column<int>(type: "INTEGER", nullable: true),
                    AgeUnit = table.Column<int>(type: "INTEGER", nullable: true),
                    Sex = table.Column<int>(type: "INTEGER", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    HeightCm = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    MedicalHistory = table.Column<string>(type: "TEXT", nullable: true),
                    ConcomitantMedications = table.Column<string>(type: "TEXT", nullable: true),
                    IsPregnant = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastMenstrualPeriod = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEPatient", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AEPatient_AdverseEvent_AdverseEventId",
                        column: x => x.AdverseEventId,
                        principalTable: "AdverseEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AEReporter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdverseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReporterName = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    Institution = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Qualification = table.Column<int>(type: "INTEGER", nullable: true),
                    ReportDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEReporter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AEReporter_AdverseEvent_AdverseEventId",
                        column: x => x.AdverseEventId,
                        principalTable: "AdverseEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AESuspectProduct",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdverseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProductName = table.Column<string>(type: "TEXT", nullable: true),
                    ARTGNumber = table.Column<string>(type: "TEXT", nullable: true),
                    BatchNumber = table.Column<string>(type: "TEXT", nullable: true),
                    RouteOfAdministration = table.Column<string>(type: "TEXT", nullable: true),
                    Dose = table.Column<string>(type: "TEXT", nullable: true),
                    DoseUnit = table.Column<string>(type: "TEXT", nullable: true),
                    DoseFrequency = table.Column<string>(type: "TEXT", nullable: true),
                    Indication = table.Column<string>(type: "TEXT", nullable: true),
                    TreatmentStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TreatmentEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActionTaken = table.Column<int>(type: "INTEGER", nullable: true),
                    DechallengeResult = table.Column<int>(type: "INTEGER", nullable: true),
                    RechallengeResult = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AESuspectProduct", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AESuspectProduct_AdverseEvent_AdverseEventId",
                        column: x => x.AdverseEventId,
                        principalTable: "AdverseEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AESuspectProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AERegulatoryReport",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdverseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsReportableToTGA = table.Column<bool>(type: "INTEGER", nullable: true),
                    ReportingDecisionRationale = table.Column<string>(type: "TEXT", nullable: true),
                    TGASubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmissionMethod = table.Column<int>(type: "INTEGER", nullable: true),
                    TGAADRSReferenceNumber = table.Column<string>(type: "TEXT", nullable: true),
                    E2BTransmissionReference = table.Column<string>(type: "TEXT", nullable: true),
                    SevenDayDeadlineDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OverseasReportingRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverseasAuthorities = table.Column<string>(type: "TEXT", nullable: true),
                    IncludeInNextPSUR = table.Column<bool>(type: "INTEGER", nullable: false),
                    SignificantSafetyIssue = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PSURId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AERegulatoryReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AERegulatoryReport_AdverseEvent_AdverseEventId",
                        column: x => x.AdverseEventId,
                        principalTable: "AdverseEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AERegulatoryReport_PVPeriodicReport_PSURId",
                        column: x => x.PSURId,
                        principalTable: "PVPeriodicReport",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdverseEvent_ParentAEId",
                table: "AdverseEvent",
                column: "ParentAEId");

            migrationBuilder.CreateIndex(
                name: "IX_AEAttachment_AdverseEventId",
                table: "AEAttachment",
                column: "AdverseEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AEAuditLog_AdverseEventId",
                table: "AEAuditLog",
                column: "AdverseEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AEPatient_AdverseEventId",
                table: "AEPatient",
                column: "AdverseEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AERegulatoryReport_AdverseEventId",
                table: "AERegulatoryReport",
                column: "AdverseEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AERegulatoryReport_PSURId",
                table: "AERegulatoryReport",
                column: "PSURId");

            migrationBuilder.CreateIndex(
                name: "IX_AEReporter_AdverseEventId",
                table: "AEReporter",
                column: "AdverseEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AESuspectProduct_AdverseEventId",
                table: "AESuspectProduct",
                column: "AdverseEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AESuspectProduct_ProductId",
                table: "AESuspectProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PVPeriodicReport_ProductId",
                table: "PVPeriodicReport",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AEAttachment");

            migrationBuilder.DropTable(
                name: "AEAuditLog");

            migrationBuilder.DropTable(
                name: "AEPatient");

            migrationBuilder.DropTable(
                name: "AERegulatoryReport");

            migrationBuilder.DropTable(
                name: "AEReporter");

            migrationBuilder.DropTable(
                name: "AESuspectProduct");

            migrationBuilder.DropTable(
                name: "PVLiteratureSearch");

            migrationBuilder.DropTable(
                name: "PVPeriodicReport");

            migrationBuilder.DropTable(
                name: "AdverseEvent");
        }
    }
}
