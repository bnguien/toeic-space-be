using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCentersRoomsAndClassDefaultRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultRoomId",
                table: "Classes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "Centers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TimeZoneId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Centers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CenterId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.CheckConstraint("CK_Rooms_Capacity", "`Capacity` > 0");
                    table.ForeignKey(
                        name: "FK_Rooms_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClassRoomChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClassId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PreviousRoomId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    NewRoomId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ChangedBy = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassRoomChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassRoomChanges_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassRoomChanges_Rooms_NewRoomId",
                        column: x => x.NewRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassRoomChanges_Rooms_PreviousRoomId",
                        column: x => x.PreviousRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_DefaultRoomId",
                table: "Classes",
                column: "DefaultRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Centers_Status_Name",
                table: "Centers",
                columns: new[] { "Status", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoomChanges_ClassId_ChangedAt",
                table: "ClassRoomChanges",
                columns: new[] { "ClassId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoomChanges_NewRoomId",
                table: "ClassRoomChanges",
                column: "NewRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoomChanges_PreviousRoomId",
                table: "ClassRoomChanges",
                column: "PreviousRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CenterId_Name",
                table: "Rooms",
                columns: new[] { "CenterId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CenterId_Status",
                table: "Rooms",
                columns: new[] { "CenterId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Classes_Rooms_DefaultRoomId",
                table: "Classes",
                column: "DefaultRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classes_Rooms_DefaultRoomId",
                table: "Classes");

            migrationBuilder.DropTable(
                name: "ClassRoomChanges");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Centers");

            migrationBuilder.DropIndex(
                name: "IX_Classes_DefaultRoomId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "DefaultRoomId",
                table: "Classes");
        }
    }
}
