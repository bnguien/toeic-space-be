using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_TokenHash",
                table: "UserTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTokens_TokenHash",
                table: "UserTokens");
        }
    }
}
