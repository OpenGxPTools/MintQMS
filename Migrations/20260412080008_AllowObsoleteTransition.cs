using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AllowObsoleteTransition : Migration
    {
        // Tables that carry IsLocked — must match the list in AddIsLockedTriggers.
        private static readonly string[] LockedTables =
        [
            "AppDocument",
            "Asset",
            "Process",
            "Material",
            "Service",
            "Product",
            "Customer",
            "AdverseEvent",
            "RegulatoryApplications",
            "LicenceCertificationRecords",
            "ReleaseRecords",
            "ReleaseTestResults",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replace the blanket "block all updates when locked" trigger with one that
            // allows updates that explicitly unlock the record (NEW.IsLocked = 0).
            // This lets the approval workflow transition a locked Approved record to
            // Obsolete by setting IsLocked = false at the same time.
            foreach (var table in LockedTables)
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS trg_{table}_block_update_when_locked;
                    """);

                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_block_update_when_locked
                    BEFORE UPDATE ON "{table}"
                    WHEN OLD."IsLocked" = 1 AND NEW."IsLocked" = 1
                    BEGIN
                        SELECT RAISE(ABORT, 'Record in {table} is locked and cannot be modified.');
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the original strict trigger.
            foreach (var table in LockedTables)
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS trg_{table}_block_update_when_locked;
                    """);

                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_block_update_when_locked
                    BEFORE UPDATE ON "{table}"
                    WHEN OLD."IsLocked" = 1
                    BEGIN
                        SELECT RAISE(ABORT, 'Record in {table} is locked and cannot be modified.');
                    END;
                    """);
            }
        }
    }
}
