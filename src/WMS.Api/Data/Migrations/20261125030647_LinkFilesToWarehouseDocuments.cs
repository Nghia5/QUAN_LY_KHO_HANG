using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkFilesToWarehouseDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentId",
                table: "UploadedFiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "UploadedFiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_DocumentType_DocumentId",
                table: "UploadedFiles",
                columns: new[] { "DocumentType", "DocumentId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UploadedFiles_Document",
                table: "UploadedFiles",
                sql: "([DocumentType] IS NULL AND [DocumentId] IS NULL) OR ([DocumentType] IS NOT NULL AND [DocumentType] BETWEEN 1 AND 5 AND [DocumentId] IS NOT NULL AND [DocumentId] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UploadedFiles_DocumentType_DocumentId",
                table: "UploadedFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UploadedFiles_Document",
                table: "UploadedFiles");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "UploadedFiles");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "UploadedFiles");
        }
    }
}
