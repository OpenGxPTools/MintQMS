using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTrainingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserTraining_AspNetUsers_TraineeId",
                table: "UserTraining");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserTraining",
                table: "UserTraining");

            migrationBuilder.RenameColumn(
                name: "EffectiveDate",
                table: "AppDocument",
                newName: "IssuedOn");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "UserTraining",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<string>(
                name: "CompetencyNotes",
                table: "UserTraining",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompetencyVerifiedBy",
                table: "UserTraining",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompetencyVerifiedOn",
                table: "UserTraining",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompetencyVerifiedSignature",
                table: "UserTraining",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAcknowledgedOn",
                table: "UserTraining",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReadAcknowledgedSignature",
                table: "UserTraining",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "UserTraining",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "TrainerId",
                table: "Training",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "TrainerEmail",
                table: "Training",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourceDocumentVersion",
                table: "Training",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingType",
                table: "Training",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "DocumentReviewPeriodMonths",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IssueCommentary",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuedBy",
                table: "AppDocument",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PeriodicReviewRequired",
                table: "AppDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TrainingRequired",
                table: "AppDocument",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserTraining",
                table: "UserTraining",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "DocumentTrainingRequirement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentVersion = table.Column<decimal>(type: "TEXT", nullable: false),
                    TrainingId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTrainingRequirement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingRequirement_AppDocument_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "AppDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingRequirement_Training_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "Training",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingAttachment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserTrainingId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingAttachment_UserTraining_UserTrainingId",
                        column: x => x.UserTrainingId,
                        principalTable: "UserTraining",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTraining_TraineeId",
                table: "UserTraining",
                column: "TraineeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingRequirement_DocumentId",
                table: "DocumentTrainingRequirement",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingRequirement_TrainingId",
                table: "DocumentTrainingRequirement",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingAttachment_UserTrainingId",
                table: "TrainingAttachment",
                column: "UserTrainingId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTraining_AspNetUsers_TraineeId",
                table: "UserTraining",
                column: "TraineeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Data migration: set UserTraining.Status based on parent Training.Status
            migrationBuilder.Sql(@"
                UPDATE UserTraining
                SET Status = CASE
                    WHEN (SELECT Status FROM Training WHERE Training.Id = UserTraining.TrainingId) = 1 THEN 3
                    ELSE 0
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserTraining_AspNetUsers_TraineeId",
                table: "UserTraining");

            migrationBuilder.DropTable(
                name: "DocumentTrainingRequirement");

            migrationBuilder.DropTable(
                name: "TrainingAttachment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserTraining",
                table: "UserTraining");

            migrationBuilder.DropIndex(
                name: "IX_UserTraining_TraineeId",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "CompetencyNotes",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "CompetencyVerifiedBy",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "CompetencyVerifiedOn",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "CompetencyVerifiedSignature",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "ReadAcknowledgedOn",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "ReadAcknowledgedSignature",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserTraining");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "SourceDocumentVersion",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "TrainingType",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "DocumentReviewPeriodMonths",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "IssueCommentary",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "IssuedBy",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "PeriodicReviewRequired",
                table: "AppDocument");

            migrationBuilder.DropColumn(
                name: "TrainingRequired",
                table: "AppDocument");

            migrationBuilder.RenameColumn(
                name: "IssuedOn",
                table: "AppDocument",
                newName: "EffectiveDate");

            migrationBuilder.AlterColumn<string>(
                name: "TrainerId",
                table: "Training",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TrainerEmail",
                table: "Training",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserTraining",
                table: "UserTraining",
                columns: new[] { "TraineeId", "TrainingId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserTraining_AspNetUsers_TraineeId",
                table: "UserTraining",
                column: "TraineeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
