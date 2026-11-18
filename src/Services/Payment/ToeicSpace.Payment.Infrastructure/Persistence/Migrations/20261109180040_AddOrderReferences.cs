using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_CouponReservations_PurchaseOrders_OrderId",
                table: "CouponReservations",
                column: "OrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_PurchaseOrders_OrderId",
                table: "PaymentTransactions",
                column: "OrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CouponReservations_PurchaseOrders_OrderId",
                table: "CouponReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_PurchaseOrders_OrderId",
                table: "PaymentTransactions");
        }
    }
}
