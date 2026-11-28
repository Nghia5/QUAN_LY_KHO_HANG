using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockTransfers_Status",
                table: "StockTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues");

            migrationBuilder.CreateTable(
                name: "Stocktakes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScopeLocationIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SnapshotBy = table.Column<int>(type: "int", nullable: true),
                    SnapshotAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdjustedBy = table.Column<int>(type: "int", nullable: true),
                    AdjustedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Stocktakes", x => x.Id);
                    table.UniqueConstraint("AK_Stocktakes_Id_WarehouseId", x => new { x.Id, x.WarehouseId });
                    table.CheckConstraint("CK_Stocktakes_Status", "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received', 'Adjusted')");
                    table.ForeignKey(
                        name: "FK_Stocktakes_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StocktakeId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Difference = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true, computedColumnSql: "[CountedQuantity] - [SystemQuantity]", stored: true),
                    IsAddedDuringCount = table.Column<bool>(type: "bit", nullable: false),
                    CountedBy = table.Column<int>(type: "int", nullable: true),
                    CountedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeLines", x => x.Id);
                    table.CheckConstraint("CK_StocktakeLines_Counted_NonNegative", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
                    table.CheckConstraint("CK_StocktakeLines_System_NonNegative", "[SystemQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_StocktakeLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StocktakeLines_Stocktakes_StocktakeId_WarehouseId",
                        columns: x => new { x.StocktakeId, x.WarehouseId },
                        principalTable: "Stocktakes",
                        principalColumns: new[] { "Id", "WarehouseId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StocktakeLines_WarehouseLocations_LocationId_WarehouseId",
                        columns: x => new { x.LocationId, x.WarehouseId },
                        principalTable: "WarehouseLocations",
                        principalColumns: new[] { "Id", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockTransfers_Status",
                table: "StockTransfers",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received', 'Adjusted')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received', 'Adjusted')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received', 'Adjusted')");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLines_LocationId",
                table: "StocktakeLines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLines_LocationId_WarehouseId",
                table: "StocktakeLines",
                columns: new[] { "LocationId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLines_ProductId",
                table: "StocktakeLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLines_StocktakeId_ProductId_LocationId",
                table: "StocktakeLines",
                columns: new[] { "StocktakeId", "ProductId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLines_StocktakeId_WarehouseId",
                table: "StocktakeLines",
                columns: new[] { "StocktakeId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_CreatedAt",
                table: "Stocktakes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_DocumentNo",
                table: "Stocktakes",
                column: "DocumentNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_WarehouseId_Status",
                table: "Stocktakes",
                columns: new[] { "WarehouseId", "Status" });

            // Document numbers: ST-<year>-<sequence>.
            migrationBuilder.CreateSequence<long>(name: "StocktakeNumbers", schema: "dbo", startValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(name: "StocktakeNumbers", schema: "dbo");

            migrationBuilder.DropTable(
                name: "StocktakeLines");

            migrationBuilder.DropTable(
                name: "Stocktakes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockTransfers_Status",
                table: "StockTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockTransfers_Status",
                table: "StockTransfers",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsReceipts_Status",
                table: "GoodsReceipts",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GoodsIssues_Status",
                table: "GoodsIssues",
                sql: "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled', 'Picked', 'InTransit', 'Received')");
        }
    }
}
