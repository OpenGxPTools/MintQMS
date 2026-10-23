using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddIsLockedTriggers : Migration
    {
        // Tables that carry IsLocked and must be immutable once locked.
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
            foreach (var table in LockedTables)
            {
                // Block UPDATE of any field when the record is already locked.
                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_block_update_when_locked
                    BEFORE UPDATE ON "{table}"
                    WHEN OLD."IsLocked" = 1
                    BEGIN
                        SELECT RAISE(ABORT, 'Record in {table} is locked and cannot be modified.');
                    END;
                    """);

                // Block DELETE of a locked record.
                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_block_delete_when_locked
                    BEFORE DELETE ON "{table}"
                    WHEN OLD."IsLocked" = 1
                    BEGIN
                        SELECT RAISE(ABORT, 'Record in {table} is locked and cannot be deleted.');
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in LockedTables)
            {
                migrationBuilder.Sql($"""DROP TRIGGER IF EXISTS trg_{table}_block_update_when_locked;""");
                migrationBuilder.Sql($"""DROP TRIGGER IF EXISTS trg_{table}_block_delete_when_locked;""");
            }
        }
    }
}
