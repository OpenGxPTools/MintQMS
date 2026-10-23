using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagingSiteAndTestingLabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TestingLaboratory",
                table: "ReleaseRecords",
                newName: "PackagingSite");

            migrationBuilder.RenameColumn(
                name: "LabAccreditationRef",
                table: "ReleaseRecords",
                newName: "PackagingSiteGmpRef");

            migrationBuilder.CreateTable(
                name: "ReleaseTestingLabs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    LaboratoryName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GmpLicenceRef = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseTestingLabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseTestingLabs_ReleaseRecords_ReleaseRecordId",
                        column: x => x.ReleaseRecordId,
                        principalTable: "ReleaseRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTestingLabs_ReleaseRecordId",
                table: "ReleaseTestingLabs",
                column: "ReleaseRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReleaseTestingLabs");

            migrationBuilder.RenameColumn(
                name: "PackagingSiteGmpRef",
                table: "ReleaseRecords",
                newName: "LabAccreditationRef");

            migrationBuilder.RenameColumn(
                name: "PackagingSite",
                table: "ReleaseRecords",
                newName: "TestingLaboratory");
        }
    }
}
