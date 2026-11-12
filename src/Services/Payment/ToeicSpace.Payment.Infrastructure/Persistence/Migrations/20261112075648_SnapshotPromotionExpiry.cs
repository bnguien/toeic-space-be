using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotPromotionExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PromotionExpiresAt",
                table: "PurchaseOrders",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromotionExpiresAt",
                table: "PurchaseOrders");
        }
    }
}
