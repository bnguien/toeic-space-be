using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportMultipleLessonVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileSignature",
                table: "LessonUploads",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LessonVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LessonId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ObjectKey = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonVideos_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                INSERT INTO `LessonVideos` (`Id`, `LessonId`, `FileName`, `ObjectKey`, `ContentType`, `FileSize`, `DurationSeconds`, `SortOrder`, `CreatedAt`)
                SELECT UUID(), l.`Id`, COALESCE(u.`FileName`, SUBSTRING_INDEX(l.`VideoKey`, '/', -1)),
                       l.`VideoKey`, COALESCE(u.`ContentType`, 'video/mp4'), COALESCE(u.`SizeBytes`, 0),
                       l.`DurationSeconds`, 1, l.`CreatedAt`
                FROM `Lessons` l LEFT JOIN `LessonUploads` u ON u.`Key` = l.`VideoKey`
                WHERE l.`VideoKey` IS NOT NULL;
                """);

            migrationBuilder.DropColumn(name: "VideoKey", table: "Lessons");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVideos_LessonId_SortOrder",
                table: "LessonVideos",
                columns: new[] { "LessonId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonVideos_ObjectKey",
                table: "LessonVideos",
                column: "ObjectKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoKey",
                table: "Lessons",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE `Lessons` l
                JOIN (SELECT `LessonId`, MIN(`SortOrder`) AS `FirstOrder` FROM `LessonVideos` GROUP BY `LessonId`) v
                  ON v.`LessonId` = l.`Id`
                JOIN `LessonVideos` mv ON mv.`LessonId` = v.`LessonId` AND mv.`SortOrder` = v.`FirstOrder`
                SET l.`VideoKey` = mv.`ObjectKey`;
                """);

            migrationBuilder.DropTable(
                name: "LessonVideos");

            migrationBuilder.DropColumn(
                name: "FileSignature",
                table: "LessonUploads");

        }
    }
}
