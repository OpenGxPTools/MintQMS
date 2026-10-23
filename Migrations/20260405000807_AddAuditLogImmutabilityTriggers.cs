using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogImmutabilityTriggers : Migration
    {
        // All audit log tables — once written, records must never be modified or deleted.
        // Note: MasterDataAuditLog is excluded here because its table creation migration
        // was scaffolded but never filled in. Triggers for that table will be added once
        // the table itself is created.
        private static readonly string[] AuditLogTables =
        [
            "DocumentAuditLog",
            "UserAuditLog",
            "ChangeAuditLog",
            "CapaAuditLog",
            "DeviationAuditLog",
            "ComplaintAuditLog",
            "RiskAuditLog",
            "AuditAuditLog",
            "MaintenanceAuditLog",
            "SupplierAuditLog",
            "AEAuditLog",
            "RIAuditLogs",
            "LCAuditLogs",
            "ReleaseAuditEntries",
            "SettingsAuditLog",
            "LoginAuditLog",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AuditLogTables)
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
            foreach (var table in AuditLogTables)
            {
                migrationBuilder.Sql($"""DROP TRIGGER IF EXISTS trg_{table}_block_update;""");
                migrationBuilder.Sql($"""DROP TRIGGER IF EXISTS trg_{table}_block_delete;""");
            }
        }
    }
}
