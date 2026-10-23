using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class FixCriticalComplaintResolutionDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Correct the existing SystemSettings row: the column was added with defaultValue: 0
            // but the intended default is 7 days (GMP expedited resolution target).
            // Only update rows that are still at the migration default to avoid overwriting
            // any value the administrator may have already set.
            migrationBuilder.Sql(
                "UPDATE SystemSettings SET CriticalComplaintResolutionDays = 7 " +
                "WHERE Id = 1 AND CriticalComplaintResolutionDays = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE SystemSettings SET CriticalComplaintResolutionDays = 0 " +
                "WHERE Id = 1 AND CriticalComplaintResolutionDays = 7;");
        }
    }
}
