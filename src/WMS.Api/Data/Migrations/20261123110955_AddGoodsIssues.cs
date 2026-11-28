using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoodsIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_GoodsIssues", x => x.Id);
                    table.UniqueConstraint("AK_GoodsIssues_Id_WarehouseId", x => new { x.Id, x.WarehouseId });
                    table.CheckConstraint("CK_GoodsIssues_Status", "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_GoodsIssues_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsIssueLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoodsIssueId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsIssueLines", x => x.Id);
                    table.CheckConstraint("CK_GoodsIssueLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_GoodsIssueLines_GoodsIssues_GoodsIssueId_WarehouseId",
                        columns: x => new { x.GoodsIssueId, x.WarehouseId },
                        principalTable: "GoodsIssues",
                        principalColumns: new[] { "Id", "WarehouseId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsIssueLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsIssueLines_WarehouseLocations_LocationId_WarehouseId",
                        columns: x => new { x.LocationId, x.WarehouseId },
                        principalTable: "WarehouseLocations",
                        principalColumns: new[] { "Id", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueLines_GoodsIssueId_WarehouseId",
                table: "GoodsIssueLines",
                columns: new[] { "GoodsIssueId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueLines_LocationId_WarehouseId",
                table: "GoodsIssueLines",
                columns: new[] { "LocationId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueLines_ProductId",
                table: "GoodsIssueLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_CreatedAt",
                table: "GoodsIssues",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_CustomerId",
                table: "GoodsIssues",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_DocumentNo",
                table: "GoodsIssues",
                column: "DocumentNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_IssueDate",
                table: "GoodsIssues",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_WarehouseId_Status",
                table: "GoodsIssues",
                columns: new[] { "WarehouseId", "Status" });

            // Document numbers: GI-<year>-<sequence>.
            migrationBuilder.CreateSequence<long>(name: "GoodsIssueNumbers", schema: "dbo", startValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(name: "GoodsIssueNumbers", schema: "dbo");

            migrationBuilder.DropTable(
                name: "GoodsIssueLines");

            migrationBuilder.DropTable(
                name: "GoodsIssues");
        }
    }
}
