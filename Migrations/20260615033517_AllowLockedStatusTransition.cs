using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AllowLockedStatusTransition : Migration
    {
        // Tables that carry IsLocked — must match the list in AddIsLockedTriggers.
        // Every one of these also carries a "Status" column.
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
            // The previous trigger (AllowObsoleteTransition) blocked every update to a
            // locked record unless it unlocked (NEW.IsLocked = 0). That prevented
            // legitimate locked -> locked workflow transitions, e.g. an Adverse Event
            // moving Approved -> Closed (both immutable, locked states), and likewise
            // RI Approved -> Suspended and LC Issued/Active -> Suspended/Expired/Withdrawn.
            //
            // Refine the rule: a locked record that stays locked may still change Status
            // (a controlled workflow transition), but a pure data edit — locked, staying
            // locked, with Status unchanged — remains blocked. Immutability of the
            // record's data is preserved; lifecycle transitions are allowed.
            foreach (var table in LockedTables)
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS trg_{table}_block_update_when_locked;
                    """);

                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_block_update_when_locked
                    BEFORE UPDATE ON "{table}"
                    WHEN OLD."IsLocked" = 1 AND NEW."IsLocked" = 1 AND NEW."Status" = OLD."Status"
                    BEGIN
                        SELECT RAISE(ABORT, 'Record in {table} is locked and cannot be modified.');
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the AllowObsoleteTransition trigger (unlock-only exception).
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
    }
}
