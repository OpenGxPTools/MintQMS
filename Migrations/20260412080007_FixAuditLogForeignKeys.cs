using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class FixAuditLogForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditAuditLog_Audit_AuditId",
                table: "AuditAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ChangeAuditLog_Change_ChangeId",
                table: "ChangeAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ComplaintAuditLog_Complaint_ComplaintId",
                table: "ComplaintAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviationAuditLog_Deviation_DeviationId",
                table: "DeviationAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceAuditLog_Maintenance_MaintenanceId",
                table: "MaintenanceAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskAuditLog_Risk_RiskId",
                table: "RiskAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierAuditLog_Supplier_SupplierId",
                table: "SupplierAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_SupplierAuditLog_SupplierId",
                table: "SupplierAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_RiskAuditLog_RiskId",
                table: "RiskAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceAuditLog_MaintenanceId",
                table: "MaintenanceAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_DeviationAuditLog_DeviationId",
                table: "DeviationAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintAuditLog_ComplaintId",
                table: "ComplaintAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_ChangeAuditLog_ChangeId",
                table: "ChangeAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditAuditLog_AuditId",
                table: "AuditAuditLog");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "SupplierAuditLog");

            migrationBuilder.DropColumn(
                name: "RiskId",
                table: "RiskAuditLog");

            migrationBuilder.DropColumn(
                name: "MaintenanceId",
                table: "MaintenanceAuditLog");

            migrationBuilder.DropColumn(
                name: "DeviationId",
                table: "DeviationAuditLog");

            migrationBuilder.DropColumn(
                name: "ComplaintId",
                table: "ComplaintAuditLog");

            migrationBuilder.DropColumn(
                name: "ChangeId",
                table: "ChangeAuditLog");

            migrationBuilder.DropColumn(
                name: "AuditId",
                table: "AuditAuditLog");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAuditLog_ParentId",
                table: "SupplierAuditLog",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditLog_ParentId",
                table: "RiskAuditLog",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAuditLog_ParentId",
                table: "MaintenanceAuditLog",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationAuditLog_ParentId",
                table: "DeviationAuditLog",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintAuditLog_ParentId",
                table: "ComplaintAuditLog",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeAuditLog_ParentId",
                table: "ChangeAuditLog",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditAuditLog_ParentId",
                table: "AuditAuditLog",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditAuditLog_Audit_ParentId",
                table: "AuditAuditLog",
                column: "ParentId",
                principalTable: "Audit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeAuditLog_Change_ParentId",
                table: "ChangeAuditLog",
                column: "ParentId",
                principalTable: "Change",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ComplaintAuditLog_Complaint_ParentId",
                table: "ComplaintAuditLog",
                column: "ParentId",
                principalTable: "Complaint",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviationAuditLog_Deviation_ParentId",
                table: "DeviationAuditLog",
                column: "ParentId",
                principalTable: "Deviation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceAuditLog_Maintenance_ParentId",
                table: "MaintenanceAuditLog",
                column: "ParentId",
                principalTable: "Maintenance",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RiskAuditLog_Risk_ParentId",
                table: "RiskAuditLog",
                column: "ParentId",
                principalTable: "Risk",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierAuditLog_Supplier_ParentId",
                table: "SupplierAuditLog",
                column: "ParentId",
                principalTable: "Supplier",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditAuditLog_Audit_ParentId",
                table: "AuditAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ChangeAuditLog_Change_ParentId",
                table: "ChangeAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ComplaintAuditLog_Complaint_ParentId",
                table: "ComplaintAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviationAuditLog_Deviation_ParentId",
                table: "DeviationAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceAuditLog_Maintenance_ParentId",
                table: "MaintenanceAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskAuditLog_Risk_ParentId",
                table: "RiskAuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierAuditLog_Supplier_ParentId",
                table: "SupplierAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_SupplierAuditLog_ParentId",
                table: "SupplierAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_RiskAuditLog_ParentId",
                table: "RiskAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceAuditLog_ParentId",
                table: "MaintenanceAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_DeviationAuditLog_ParentId",
                table: "DeviationAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintAuditLog_ParentId",
                table: "ComplaintAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_ChangeAuditLog_ParentId",
                table: "ChangeAuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditAuditLog_ParentId",
                table: "AuditAuditLog");

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "SupplierAuditLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiskId",
                table: "RiskAuditLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaintenanceId",
                table: "MaintenanceAuditLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeviationId",
                table: "DeviationAuditLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplaintId",
                table: "ComplaintAuditLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChangeId",
                table: "ChangeAuditLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuditId",
                table: "AuditAuditLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAuditLog_SupplierId",
                table: "SupplierAuditLog",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditLog_RiskId",
                table: "RiskAuditLog",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAuditLog_MaintenanceId",
                table: "MaintenanceAuditLog",
                column: "MaintenanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationAuditLog_DeviationId",
                table: "DeviationAuditLog",
                column: "DeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintAuditLog_ComplaintId",
                table: "ComplaintAuditLog",
                column: "ComplaintId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeAuditLog_ChangeId",
                table: "ChangeAuditLog",
                column: "ChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditAuditLog_AuditId",
                table: "AuditAuditLog",
                column: "AuditId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditAuditLog_Audit_AuditId",
                table: "AuditAuditLog",
                column: "AuditId",
                principalTable: "Audit",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeAuditLog_Change_ChangeId",
                table: "ChangeAuditLog",
                column: "ChangeId",
                principalTable: "Change",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ComplaintAuditLog_Complaint_ComplaintId",
                table: "ComplaintAuditLog",
                column: "ComplaintId",
                principalTable: "Complaint",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviationAuditLog_Deviation_DeviationId",
                table: "DeviationAuditLog",
                column: "DeviationId",
                principalTable: "Deviation",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceAuditLog_Maintenance_MaintenanceId",
                table: "MaintenanceAuditLog",
                column: "MaintenanceId",
                principalTable: "Maintenance",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RiskAuditLog_Risk_RiskId",
                table: "RiskAuditLog",
                column: "RiskId",
                principalTable: "Risk",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierAuditLog_Supplier_SupplierId",
                table: "SupplierAuditLog",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");
        }
    }
}
