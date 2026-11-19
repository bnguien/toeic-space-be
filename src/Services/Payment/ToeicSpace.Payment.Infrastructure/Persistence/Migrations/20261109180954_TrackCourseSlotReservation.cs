using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackCourseSlotReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SlotReservationId",
                table: "PurchaseOrders",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlotReservationId",
                table: "PurchaseOrders");
        }
    }
}
