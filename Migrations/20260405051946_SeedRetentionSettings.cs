using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class SeedRetentionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gap 13: Seed default retention period values into the existing SystemSettings row.
            migrationBuilder.Sql(@"
                UPDATE SystemSettings SET
                    RetentionHoldCooldownDays = 30,
                    CapaRetentionYears        = 5,
                    DeviationRetentionYears   = 5,
                    ChangeRetentionYears      = 5,
                    ComplaintRetentionYears   = 5,
                    RiskRetentionYears        = 5,
                    AuditRetentionYears       = 5,
                    MaintenanceRetentionYears = 5,
                    TrainingRetentionYears    = 5,
                    DocumentRetentionYears    = 5,
                    RIRetentionYears          = 10,
                    LCRetentionYears          = 10
                WHERE Id = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE SystemSettings SET
                    RetentionHoldCooldownDays = 0,
                    CapaRetentionYears = 0, DeviationRetentionYears = 0,
                    ChangeRetentionYears = 0, ComplaintRetentionYears = 0,
                    RiskRetentionYears = 0, AuditRetentionYears = 0,
                    MaintenanceRetentionYears = 0, TrainingRetentionYears = 0,
                    DocumentRetentionYears = 0, RIRetentionYears = 0, LCRetentionYears = 0
                WHERE Id = 1;");
        }
    }
}
