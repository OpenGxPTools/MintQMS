using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingAuditLogTriggers : Migration
    {
        // MasterDataAuditLog table was never created (its creation migration was scaffolded empty).
        // DisposalAuditLog existed but lacked immutability triggers.
        // This migration creates the MasterDataAuditLog table and adds immutability triggers
        // for both tables.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create MasterDataAuditLog table (omitted from original migration)
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "MasterDataAuditLog" (
                    "Id"          INTEGER NOT NULL CONSTRAINT "PK_MasterDataAuditLog" PRIMARY KEY AUTOINCREMENT,
                    "EntityType"  TEXT    NOT NULL,
                    "ParentId"    INTEGER NOT NULL,
                    "Action"      TEXT    NOT NULL,
                    "FromStatus"  TEXT    NULL,
                    "ToStatus"    TEXT    NULL,
                    "PerformedBy" TEXT    NOT NULL,
                    "PerformedOn" TEXT    NOT NULL,
                    "Notes"       TEXT    NULL
                );
                """);

            // Add immutability triggers for both tables
            foreach (var table in new[] { "MasterDataAuditLog", "DisposalAuditLog" })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_block_update
                    BEFORE UPDATE ON "{table}"
                    BEGIN
                        SELECT RAISE(ABORT, 'Audit log records in {table} are immutable and cannot be modified.');
                    END;
                    """);

                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_block_delete
                    BEFORE DELETE ON "{table}"
                    BEGIN
                        SELECT RAISE(ABORT, 'Audit log records in {table} are immutable and cannot be deleted.');
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "MasterDataAuditLog", "DisposalAuditLog" })
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{table}_block_update;");
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{table}_block_delete;");
            }
            migrationBuilder.DropTable("MasterDataAuditLog");
        }
    }
}
