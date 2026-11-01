using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassEnrollmentsAndStudentReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClassId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    StudentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnrolledAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EnrolledBy = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WithdrawnBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CurrentStudentId = table.Column<Guid>(type: "char(36)", nullable: true, computedColumnSql: "CASE WHEN `Status` = 'Active' AND `WithdrawnAt` IS NULL THEN `StudentId` ELSE NULL END", stored: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassEnrollments_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentReadModels",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsVerified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsEligible = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastEventId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SynchronizedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentReadModels", x => x.StudentId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClassEnrollments_ClassId_CurrentStudentId",
                table: "ClassEnrollments",
                columns: new[] { "ClassId", "CurrentStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassEnrollments_ClassId_StudentId_EnrolledAt",
                table: "ClassEnrollments",
                columns: new[] { "ClassId", "StudentId", "EnrolledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentReadModels_IsEligible_Name",
                table: "StudentReadModels",
                columns: new[] { "IsEligible", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassEnrollments");

            migrationBuilder.DropTable(
                name: "StudentReadModels");
        }
    }
}
