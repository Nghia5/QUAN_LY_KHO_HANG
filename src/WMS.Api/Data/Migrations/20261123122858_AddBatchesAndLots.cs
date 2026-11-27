using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchesAndLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippedLots",
                table: "StockTransferLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "StockMovements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "GoodsIssueLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Inventories_Id_ProductId",
                table: "Inventories",
                columns: new[] { "Id", "ProductId" });

            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    BatchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                    table.UniqueConstraint("AK_Batches_Id_ProductId", x => new { x.Id, x.ProductId });
                    table.CheckConstraint("CK_Batches_Expiry_After_Manufacture", "[ExpiryDate] IS NULL OR [ManufactureDate] IS NULL OR [ExpiryDate] > [ManufactureDate]");
                    table.ForeignKey(
                        name: "FK_Batches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLots", x => x.Id);
                    table.CheckConstraint("CK_InventoryLots_Quantity_NonNegative", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryLots_Batches_BatchId_ProductId",
                        columns: x => new { x.BatchId, x.ProductId },
                        principalTable: "Batches",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Inventories_InventoryId_ProductId",
                        columns: x => new { x.InventoryId, x.ProductId },
                        principalTable: "Inventories",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueLines_BatchId_ProductId",
                table: "GoodsIssueLines",
                columns: new[] { "BatchId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_Batches_ExpiryDate",
                table: "Batches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_ProductId_BatchCode",
                table: "Batches",
                columns: new[] { "ProductId", "BatchCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_BatchId",
                table: "InventoryLots",
                column: "BatchId")
                .Annotation("SqlServer:Include", new[] { "Quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_BatchId_ProductId",
                table: "InventoryLots",
                columns: new[] { "BatchId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_InventoryId_BatchId",
                table: "InventoryLots",
                columns: new[] { "InventoryId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_InventoryId_ProductId",
                table: "InventoryLots",
                columns: new[] { "InventoryId", "ProductId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsIssueLines_Batches_BatchId_ProductId",
                table: "GoodsIssueLines",
                columns: new[] { "BatchId", "ProductId" },
                principalTable: "Batches",
                principalColumns: new[] { "Id", "ProductId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Batches_BatchId",
                table: "StockMovements",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsIssueLines_Batches_BatchId_ProductId",
                table: "GoodsIssueLines");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Batches_BatchId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "InventoryLots");

            migrationBuilder.DropTable(
                name: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Inventories_Id_ProductId",
                table: "Inventories");

            migrationBuilder.DropIndex(
                name: "IX_GoodsIssueLines_BatchId_ProductId",
                table: "GoodsIssueLines");

            migrationBuilder.DropColumn(
                name: "ShippedLots",
                table: "StockTransferLines");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "GoodsIssueLines");
        }
    }
}
