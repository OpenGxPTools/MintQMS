using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class AddDisposalAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gap 13: Immutability triggers on DisposalAuditLog (same pattern as Gap 8).
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_DisposalAuditLog_block_update
                BEFORE UPDATE ON DisposalAuditLog
                BEGIN
                    SELECT RAISE(ABORT, 'DisposalAuditLog records are immutable and cannot be modified.');
                END;");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_DisposalAuditLog_block_delete
                BEFORE DELETE ON DisposalAuditLog
                BEGIN
                    SELECT RAISE(ABORT, 'DisposalAuditLog records are immutable and cannot be deleted.');
                END;");

            // Gap 13: Triggers to prevent disposal (IsDeleted=true) of records with active Retention Hold.
            // Applied to all 11 retention-tracked tables.
            var tables = new[]
            {
                ("Capa",                     "CapaId"),
                ("Deviation",                "DeviationId"),
                ("Change",                   "ChangeId"),
                ("Complaint",                "ComplaintId"),
                ("Risk",                     "RiskId"),
                ("Audit",                    "Id"),
                ("Maintenance",              "Id"),
                ("Training",                 "TrainingId"),
                ("AppDocument",              "DocumentId"),
                ("RegulatoryApplications",   "RINumber"),
                ("LicenceCertificationRecords", "LCNumber")
            };

            foreach (var (table, _) in tables)
            {
                migrationBuilder.Sql($@"
                    CREATE TRIGGER trg_{table}_block_dispose_on_hold
                    BEFORE UPDATE ON {table}
                    WHEN NEW.IsDeleted = 1 AND OLD.RetentionExtended = 1
                    BEGIN
                        SELECT RAISE(ABORT, 'Record has an active Retention Hold and cannot be disposed.');
                    END;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_DisposalAuditLog_block_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_DisposalAuditLog_block_delete;");

            var tables = new[]
            {
                "Capa", "Deviation", "Change", "Complaint", "Risk",
                "Audit", "Maintenance", "Training", "AppDocument",
                "RegulatoryApplications", "LicenceCertificationRecords"
            };
            foreach (var table in tables)
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{table}_block_dispose_on_hold;");
        }
    }
}
