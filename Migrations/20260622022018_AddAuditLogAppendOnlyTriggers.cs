using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogAppendOnlyTriggers : Migration
    {
        // Audit-trail tables that must be append-only (insert + read, never update or delete).
        // Making them immutable at the database tier strengthens Part 11 §11.10(e) tamper-resistance:
        // the audit trail cannot be altered by any path, including future code or ad-hoc queries.
        // All of these are insert-only in the application; the foreign keys to their parents were
        // already removed (FixAuditLogForeignKeys), so no cascade-delete reaches them.
        private static readonly string[] AuditLogTables =
        [
            "AEAuditLog",
            "AuditAuditLog",
            "CapaAuditLog",
            "ChangeAuditLog",
            "ComplaintAuditLog",
            "DeviationAuditLog",
            "DisposalAuditLog",
            "DocumentAuditLog",
            "LCAuditLogs",
            "LoginAuditLog",
            "MaintenanceAuditLog",
            "MasterDataAuditLog",
            "PVLiteratureAuditLog",
            "PVPsurAuditLog",
            "PVSignalAuditLog",
            "ReleaseAuditEntries",
            "ReleaseCertSettingAuditLogs",
            "RIAuditLogs",
            "RiskAuditLog",
            "SettingsAuditLog",
            "SupplierAuditLog",
            "UserAuditLog",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AuditLogTables)
            {
                // Block any UPDATE of an existing audit-trail row.
                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_append_only_update
                    BEFORE UPDATE ON "{table}"
                    BEGIN
                        SELECT RAISE(ABORT, 'Audit-trail records in {table} are append-only and cannot be modified.');
                    END;
                    """);

                // Block any DELETE of an audit-trail row.
                migrationBuilder.Sql($"""
                    CREATE TRIGGER IF NOT EXISTS trg_{table}_append_only_delete
                    BEFORE DELETE ON "{table}"
                    BEGIN
                        SELECT RAISE(ABORT, 'Audit-trail records in {table} are append-only and cannot be deleted.');
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AuditLogTables)
            {
                migrationBuilder.Sql($"""DROP TRIGGER IF EXISTS trg_{table}_append_only_update;""");
                migrationBuilder.Sql($"""DROP TRIGGER IF EXISTS trg_{table}_append_only_delete;""");
            }
        }
    }
}
