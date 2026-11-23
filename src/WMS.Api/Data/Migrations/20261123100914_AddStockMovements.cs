using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_After_NonNegative", "[QuantityAfter] >= 0");
                    table.CheckConstraint("CK_StockMovements_Balance", "[QuantityAfter] = [QuantityBefore] + [Quantity]");
                    table.CheckConstraint("CK_StockMovements_Before_NonNegative", "[QuantityBefore] >= 0");
                    table.CheckConstraint("CK_StockMovements_Direction", "([MovementType] IN ('Receipt', 'TransferIn', 'AdjustmentIn', 'StocktakeIn') AND [Quantity] > 0) OR ([MovementType] IN ('Issue', 'TransferOut', 'AdjustmentOut', 'StocktakeOut') AND [Quantity] < 0)");
                    table.CheckConstraint("CK_StockMovements_Quantity_NonZero", "[Quantity] <> 0");
                    table.ForeignKey(
                        name: "FK_StockMovements_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_WarehouseLocations_LocationId_WarehouseId",
                        columns: x => new { x.LocationId, x.WarehouseId },
                        principalTable: "WarehouseLocations",
                        principalColumns: new[] { "Id", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryId_Id",
                table: "StockMovements",
                columns: new[] { "InventoryId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LocationId",
                table: "StockMovements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LocationId_WarehouseId",
                table: "StockMovements",
                columns: new[] { "LocationId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReferenceType_ReferenceId",
                table: "StockMovements",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "WarehouseId", "CreatedAt" })
                .Annotation("SqlServer:Include", new[] { "ProductId", "Quantity" });

            // Append-only ledger: any UPDATE or DELETE (from EF, SQL scripts or tools) is rejected.
            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_StockMovements_Immutable]
                ON [dbo].[StockMovements]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'StockMovements is append-only: rows cannot be updated or deleted.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_Immutable];");

            migrationBuilder.DropTable(
                name: "StockMovements");
        }
    }
}
