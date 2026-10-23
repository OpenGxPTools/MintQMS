using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class UpdateServiceCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old enum: Other = 7. New enum: Other = 19. Move existing records.
            migrationBuilder.Sql("UPDATE \"Service\" SET \"Category\" = 19 WHERE \"Category\" = 7;");
            // Old enum: Logistics = 1. New enum: LogisticsTransportation = 1 (same value, display-only change).
            // Old enum: Consulting = 4. New enum: ConsultingProfessionalServices = 4 (same value, display-only change).
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Service\" SET \"Category\" = 7 WHERE \"Category\" = 19;");
        }
    }
}
