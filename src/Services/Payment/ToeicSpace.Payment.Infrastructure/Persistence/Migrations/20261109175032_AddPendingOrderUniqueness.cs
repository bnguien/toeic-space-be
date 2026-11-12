using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingOrderUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingKey",
                table: "PurchaseOrders",
                type: "varchar(65)",
                maxLength: 65,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PendingKey",
                table: "PurchaseOrders",
                column: "PendingKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PendingKey",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PendingKey",
                table: "PurchaseOrders");
        }
    }
}
