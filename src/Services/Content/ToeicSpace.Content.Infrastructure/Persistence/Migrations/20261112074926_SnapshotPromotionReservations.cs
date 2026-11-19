using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotPromotionReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PromotionReservations",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "PromotionReservations",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PromotionReservations");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "PromotionReservations");
        }
    }
}
