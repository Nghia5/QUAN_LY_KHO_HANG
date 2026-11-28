using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues");

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestinationWarehouseId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PickedBy = table.Column<int>(type: "int", nullable: true),
                    PickedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedBy = table.Column<int>(type: "int", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedBy = table.Column<int>(type: "int", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedBy = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedBy = table.Column<int>(type: "int", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<int>(type: "int", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.UniqueConstraint("AK_StockTransfers_Id_WarehouseId", x => new { x.Id, x.WarehouseId });
                    table.UniqueConstraint("AK_StockTransfers_Id_WarehouseId_DestinationWarehouseId", x => new { x.Id, x.WarehouseId, x.DestinationWarehouseId });
                    table.CheckConstraint("CK_StockTransfers_Status", "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received')");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTransferId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    DestinationWarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    FromLocationId = table.Column<int>(type: "int", nullable: false),
                    ToLocationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferLines", x => x.Id);
                    table.CheckConstraint("CK_StockTransferLines_DifferentLocations", "[FromLocationId] <> [ToLocationId]");
                    table.CheckConstraint("CK_StockTransferLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockTransferLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockTransfers_StockTransferId_WarehouseId_DestinationWarehouseId",
                        columns: x => new { x.StockTransferId, x.WarehouseId, x.DestinationWarehouseId },
                        principalTable: "StockTransfers",
                        principalColumns: new[] { "Id", "WarehouseId", "DestinationWarehouseId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_WarehouseLocations_FromLocationId_WarehouseId",
                        columns: x => new { x.FromLocationId, x.WarehouseId },
                        principalTable: "WarehouseLocations",
                        principalColumns: new[] { "Id", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_WarehouseLocations_ToLocationId_DestinationWarehouseId",
                        columns: x => new { x.ToLocationId, x.DestinationWarehouseId },
                        principalTable: "WarehouseLocations",
                        principalColumns: new[] { "Id", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received')");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_FromLocationId_WarehouseId",
                table: "StockTransferLines",
                columns: new[] { "FromLocationId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_ProductId",
                table: "StockTransferLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_StockTransferId_WarehouseId_DestinationWarehouseId",
                table: "StockTransferLines",
                columns: new[] { "StockTransferId", "WarehouseId", "DestinationWarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_ToLocationId_DestinationWarehouseId",
                table: "StockTransferLines",
                columns: new[] { "ToLocationId", "DestinationWarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CreatedAt",
                table: "StockTransfers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationWarehouseId_Status",
                table: "StockTransfers",
                columns: new[] { "DestinationWarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DocumentNo",
                table: "StockTransfers",
                column: "DocumentNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_WarehouseId_Status",
                table: "StockTransfers",
                columns: new[] { "WarehouseId", "Status" });

            // Document numbers: TR-<year>-<sequence>.
            migrationBuilder.CreateSequence<long>(name: "StockTransferNumbers", schema: "dbo", startValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(name: "StockTransferNumbers", schema: "dbo");

            migrationBuilder.DropTable(
                name: "StockTransferLines");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled')");
        }
    }
}
