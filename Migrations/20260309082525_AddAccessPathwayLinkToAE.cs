using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessPathwayLinkToAE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessPathwayRecordId",
                table: "AdverseEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SASReferenceNumber",
                table: "AdverseEvent",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdverseEvent_AccessPathwayRecordId",
                table: "AdverseEvent",
                column: "AccessPathwayRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdverseEvent_RIAccessPathwayRecords_AccessPathwayRecordId",
                table: "AdverseEvent",
                column: "AccessPathwayRecordId",
                principalTable: "RIAccessPathwayRecords",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdverseEvent_RIAccessPathwayRecords_AccessPathwayRecordId",
                table: "AdverseEvent");

            migrationBuilder.DropIndex(
                name: "IX_AdverseEvent_AccessPathwayRecordId",
                table: "AdverseEvent");

            migrationBuilder.DropColumn(
                name: "AccessPathwayRecordId",
                table: "AdverseEvent");

            migrationBuilder.DropColumn(
                name: "SASReferenceNumber",
                table: "AdverseEvent");
        }
    }
}
