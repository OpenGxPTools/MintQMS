using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPQRChecklistItem : Migration
    {
        private const string ItemText =
            "Most recent Product Quality Review confirmed as completed, no adverse findings impacting this batch";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert PQR checklist item into every existing release record that doesn't already have it.
            // DisplayOrder = current max + 1 per record; Status 1 = NotReviewed; IsBlocking/IsRequired = 0/1.
            migrationBuilder.Sql($"""
                INSERT INTO "ReleaseChecklistItems"
                    ("ReleaseRecordId", "ItemText", "Category", "DisplayOrder", "IsBlocking", "IsRequired", "Status")
                SELECT
                    r."Id",
                    '{ItemText}',
                    'Distribution',
                    COALESCE((SELECT MAX(c."DisplayOrder") FROM "ReleaseChecklistItems" c WHERE c."ReleaseRecordId" = r."Id"), 0) + 1,
                    0,
                    1,
                    1
                FROM "ReleaseRecords" r
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ReleaseChecklistItems" c
                    WHERE c."ReleaseRecordId" = r."Id"
                    AND c."ItemText" = '{ItemText}'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM "ReleaseChecklistItems"
                WHERE "ItemText" = '{ItemText}';
                """);
        }
    }
}
