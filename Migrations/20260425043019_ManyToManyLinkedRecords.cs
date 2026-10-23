using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQMS.Migrations
{
    /// <inheritdoc />
    public partial class ManyToManyLinkedRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditAsset",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditAsset", x => new { x.AuditId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_AuditAsset_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditAsset_Audit_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditMaterial",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditMaterial", x => new { x.AuditId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_AuditMaterial_Audit_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditMaterial_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditProcess",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditProcess", x => new { x.AuditId, x.ProcessId });
                    table.ForeignKey(
                        name: "FK_AuditProcess_Audit_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditProcess_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditProduct",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditProduct", x => new { x.AuditId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_AuditProduct_Audit_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditSupplier",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditSupplier", x => new { x.AuditId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_AuditSupplier_Audit_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditSupplier_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaAsset",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaAsset", x => new { x.CapaId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_CapaAsset_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaAsset_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaAudit",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaAudit", x => new { x.CapaId, x.AuditId });
                    table.ForeignKey(
                        name: "FK_CapaAudit_Audit_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaAudit_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaComplaint",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaComplaint", x => new { x.CapaId, x.ComplaintId });
                    table.ForeignKey(
                        name: "FK_CapaComplaint_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaComplaint_Complaint_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaDeviation",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaDeviation", x => new { x.CapaId, x.DeviationId });
                    table.ForeignKey(
                        name: "FK_CapaDeviation_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaDeviation_Deviation_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "Deviation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaMaterial",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaMaterial", x => new { x.CapaId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_CapaMaterial_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaMaterial_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaProcess",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaProcess", x => new { x.CapaId, x.ProcessId });
                    table.ForeignKey(
                        name: "FK_CapaProcess_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaProcess_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaProduct",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaProduct", x => new { x.CapaId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_CapaProduct_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapaSupplier",
                columns: table => new
                {
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaSupplier", x => new { x.CapaId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_CapaSupplier_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapaSupplier_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeAsset",
                columns: table => new
                {
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeAsset", x => new { x.ChangeId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_ChangeAsset_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeAsset_Change_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Change",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeCapa",
                columns: table => new
                {
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeCapa", x => new { x.ChangeId, x.CapaId });
                    table.ForeignKey(
                        name: "FK_ChangeCapa_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeCapa_Change_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Change",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeMaterial",
                columns: table => new
                {
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeMaterial", x => new { x.ChangeId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_ChangeMaterial_Change_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Change",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeMaterial_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeProcess",
                columns: table => new
                {
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeProcess", x => new { x.ChangeId, x.ProcessId });
                    table.ForeignKey(
                        name: "FK_ChangeProcess_Change_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Change",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeProcess_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeProduct",
                columns: table => new
                {
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeProduct", x => new { x.ChangeId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ChangeProduct_Change_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Change",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeSupplier",
                columns: table => new
                {
                    ChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeSupplier", x => new { x.ChangeId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_ChangeSupplier_Change_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Change",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeSupplier_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplaintMaterial",
                columns: table => new
                {
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintMaterial", x => new { x.ComplaintId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_ComplaintMaterial_Complaint_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComplaintMaterial_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplaintProcess",
                columns: table => new
                {
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintProcess", x => new { x.ComplaintId, x.ProcessId });
                    table.ForeignKey(
                        name: "FK_ComplaintProcess_Complaint_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComplaintProcess_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplaintProduct",
                columns: table => new
                {
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintProduct", x => new { x.ComplaintId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ComplaintProduct_Complaint_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComplaintProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplaintSupplier",
                columns: table => new
                {
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintSupplier", x => new { x.ComplaintId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_ComplaintSupplier_Complaint_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComplaintSupplier_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviationAsset",
                columns: table => new
                {
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviationAsset", x => new { x.DeviationId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_DeviationAsset_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviationAsset_Deviation_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "Deviation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviationMaterial",
                columns: table => new
                {
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviationMaterial", x => new { x.DeviationId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_DeviationMaterial_Deviation_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "Deviation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviationMaterial_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviationProcess",
                columns: table => new
                {
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviationProcess", x => new { x.DeviationId, x.ProcessId });
                    table.ForeignKey(
                        name: "FK_DeviationProcess_Deviation_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "Deviation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviationProcess_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviationProduct",
                columns: table => new
                {
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviationProduct", x => new { x.DeviationId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_DeviationProduct_Deviation_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "Deviation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviationProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviationSupplier",
                columns: table => new
                {
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviationSupplier", x => new { x.DeviationId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_DeviationSupplier_Deviation_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "Deviation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviationSupplier_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceCapa",
                columns: table => new
                {
                    MaintenanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    CapaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceCapa", x => new { x.MaintenanceId, x.CapaId });
                    table.ForeignKey(
                        name: "FK_MaintenanceCapa_Capa_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceCapa_Maintenance_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "Maintenance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceDeviation",
                columns: table => new
                {
                    MaintenanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceDeviation", x => new { x.MaintenanceId, x.DeviationId });
                    table.ForeignKey(
                        name: "FK_MaintenanceDeviation_Deviation_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "Deviation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceDeviation_Maintenance_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "Maintenance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskAsset",
                columns: table => new
                {
                    RiskId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAsset", x => new { x.RiskId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_RiskAsset_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskAsset_Risk_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskMaterial",
                columns: table => new
                {
                    RiskId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskMaterial", x => new { x.RiskId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_RiskMaterial_Material_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Material",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskMaterial_Risk_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskProcess",
                columns: table => new
                {
                    RiskId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskProcess", x => new { x.RiskId, x.ProcessId });
                    table.ForeignKey(
                        name: "FK_RiskProcess_Process_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Process",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskProcess_Risk_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskProduct",
                columns: table => new
                {
                    RiskId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskProduct", x => new { x.RiskId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_RiskProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskProduct_Risk_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskSupplier",
                columns: table => new
                {
                    RiskId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskSupplier", x => new { x.RiskId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_RiskSupplier_Risk_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskSupplier_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditAsset_AssetId",
                table: "AuditAsset",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditMaterial_MaterialId",
                table: "AuditMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditProcess_ProcessId",
                table: "AuditProcess",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditProduct_ProductId",
                table: "AuditProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditSupplier_SupplierId",
                table: "AuditSupplier",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaAsset_AssetId",
                table: "CapaAsset",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaAudit_AuditId",
                table: "CapaAudit",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaComplaint_ComplaintId",
                table: "CapaComplaint",
                column: "ComplaintId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaDeviation_DeviationId",
                table: "CapaDeviation",
                column: "DeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaMaterial_MaterialId",
                table: "CapaMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaProcess_ProcessId",
                table: "CapaProcess",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaProduct_ProductId",
                table: "CapaProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaSupplier_SupplierId",
                table: "CapaSupplier",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeAsset_AssetId",
                table: "ChangeAsset",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeCapa_CapaId",
                table: "ChangeCapa",
                column: "CapaId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeMaterial_MaterialId",
                table: "ChangeMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeProcess_ProcessId",
                table: "ChangeProcess",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeProduct_ProductId",
                table: "ChangeProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeSupplier_SupplierId",
                table: "ChangeSupplier",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintMaterial_MaterialId",
                table: "ComplaintMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintProcess_ProcessId",
                table: "ComplaintProcess",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintProduct_ProductId",
                table: "ComplaintProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintSupplier_SupplierId",
                table: "ComplaintSupplier",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationAsset_AssetId",
                table: "DeviationAsset",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationMaterial_MaterialId",
                table: "DeviationMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationProcess_ProcessId",
                table: "DeviationProcess",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationProduct_ProductId",
                table: "DeviationProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationSupplier_SupplierId",
                table: "DeviationSupplier",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCapa_CapaId",
                table: "MaintenanceCapa",
                column: "CapaId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceDeviation_DeviationId",
                table: "MaintenanceDeviation",
                column: "DeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAsset_AssetId",
                table: "RiskAsset",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskMaterial_MaterialId",
                table: "RiskMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskProcess_ProcessId",
                table: "RiskProcess",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskProduct_ProductId",
                table: "RiskProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSupplier_SupplierId",
                table: "RiskSupplier",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditAsset");

            migrationBuilder.DropTable(
                name: "AuditMaterial");

            migrationBuilder.DropTable(
                name: "AuditProcess");

            migrationBuilder.DropTable(
                name: "AuditProduct");

            migrationBuilder.DropTable(
                name: "AuditSupplier");

            migrationBuilder.DropTable(
                name: "CapaAsset");

            migrationBuilder.DropTable(
                name: "CapaAudit");

            migrationBuilder.DropTable(
                name: "CapaComplaint");

            migrationBuilder.DropTable(
                name: "CapaDeviation");

            migrationBuilder.DropTable(
                name: "CapaMaterial");

            migrationBuilder.DropTable(
                name: "CapaProcess");

            migrationBuilder.DropTable(
                name: "CapaProduct");

            migrationBuilder.DropTable(
                name: "CapaSupplier");

            migrationBuilder.DropTable(
                name: "ChangeAsset");

            migrationBuilder.DropTable(
                name: "ChangeCapa");

            migrationBuilder.DropTable(
                name: "ChangeMaterial");

            migrationBuilder.DropTable(
                name: "ChangeProcess");

            migrationBuilder.DropTable(
                name: "ChangeProduct");

            migrationBuilder.DropTable(
                name: "ChangeSupplier");

            migrationBuilder.DropTable(
                name: "ComplaintMaterial");

            migrationBuilder.DropTable(
                name: "ComplaintProcess");

            migrationBuilder.DropTable(
                name: "ComplaintProduct");

            migrationBuilder.DropTable(
                name: "ComplaintSupplier");

            migrationBuilder.DropTable(
                name: "DeviationAsset");

            migrationBuilder.DropTable(
                name: "DeviationMaterial");

            migrationBuilder.DropTable(
                name: "DeviationProcess");

            migrationBuilder.DropTable(
                name: "DeviationProduct");

            migrationBuilder.DropTable(
                name: "DeviationSupplier");

            migrationBuilder.DropTable(
                name: "MaintenanceCapa");

            migrationBuilder.DropTable(
                name: "MaintenanceDeviation");

            migrationBuilder.DropTable(
                name: "RiskAsset");

            migrationBuilder.DropTable(
                name: "RiskMaterial");

            migrationBuilder.DropTable(
                name: "RiskProcess");

            migrationBuilder.DropTable(
                name: "RiskProduct");

            migrationBuilder.DropTable(
                name: "RiskSupplier");
        }
    }
}
