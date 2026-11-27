using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRefreshTokenTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_RefreshTokens_ExpiresAfterCreation",
                table: "RefreshTokens",
                sql: "[ExpiresAt] > [CreatedAt]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RefreshTokens_RevokedAfterCreation",
                table: "RefreshTokens",
                sql: "[RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RefreshTokens_ExpiresAfterCreation",
                table: "RefreshTokens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RefreshTokens_RevokedAfterCreation",
                table: "RefreshTokens");
        }
    }
}
