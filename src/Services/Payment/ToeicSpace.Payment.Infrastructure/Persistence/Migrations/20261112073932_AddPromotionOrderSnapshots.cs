using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionOrderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountSource",
                table: "PurchaseOrders",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "PromotionConfirmPending",
                table: "PurchaseOrders",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PromotionId",
                table: "PurchaseOrders",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "PromotionName",
                table: "PurchaseOrders",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "PromotionReleasePending",
                table: "PurchaseOrders",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestedCouponCode",
                table: "PurchaseOrders",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.Sql("UPDATE PurchaseOrders SET DiscountSource = 'Coupon', RequestedCouponCode = CouponCode WHERE CouponCode IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountSource",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PromotionConfirmPending",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PromotionName",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PromotionReleasePending",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RequestedCouponCode",
                table: "PurchaseOrders");
        }
    }
}
