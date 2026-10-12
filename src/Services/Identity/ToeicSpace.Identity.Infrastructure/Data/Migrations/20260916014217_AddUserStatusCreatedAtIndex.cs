using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStatusCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_Status_CreatedAt",
                table: "Users",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Status_CreatedAt",
                table: "Users");
        }
    }
}
