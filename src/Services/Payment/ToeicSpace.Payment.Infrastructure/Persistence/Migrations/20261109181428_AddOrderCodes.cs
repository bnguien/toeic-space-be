using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderCode",
                table: "PurchaseOrders",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE PurchaseOrders SET OrderCode = CONCAT('ORD', UPPER(SUBSTRING(REPLACE(Id, '-', ''), 1, 16))) WHERE OrderCode IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "OrderCode",
                table: "PurchaseOrders",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderCode",
                table: "PurchaseOrders",
                column: "OrderCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_OrderCode",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "OrderCode",
                table: "PurchaseOrders");
        }
    }
}
