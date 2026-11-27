using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <summary>
    /// Links GoodsReceipts.SupplierId to the Suppliers table (created by the AddSupplier migration of the Supplier module).
    /// Receipts created before suppliers existed may hold ids that match no supplier: those ids are cleared
    /// (the free-text SupplierName is kept) so the foreign key can be added.
    /// </summary>
    public partial class LinkGoodsReceiptSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [GoodsReceipts]
                SET [SupplierId] = NULL
                WHERE [SupplierId] IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [Suppliers] s WHERE s.[Id] = [GoodsReceipts].[SupplierId]);
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Suppliers_SupplierId",
                table: "GoodsReceipts",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Suppliers_SupplierId",
                table: "GoodsReceipts");
        }
    }
}