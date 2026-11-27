using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUserSoftDeleteState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_SoftDeleteState",
                table: "Users",
                sql: "[DeletedAt] IS NULL OR ([IsActive] = 0 AND [DeletedBy] IS NOT NULL AND [DeletedBy] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_SoftDeleteState",
                table: "Users");
        }
    }
}
