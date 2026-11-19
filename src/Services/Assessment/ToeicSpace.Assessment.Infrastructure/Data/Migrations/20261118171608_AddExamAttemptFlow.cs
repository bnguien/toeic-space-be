using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Assessment.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAttemptFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "ToeicAttempts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ListeningCorrect",
                table: "ToeicAttempts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReadingCorrect",
                table: "ToeicAttempts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedParts",
                table: "ToeicAttempts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitSeconds",
                table: "ToeicAttempts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ToeicAttempts",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedAt",
                table: "ToeicAttemptAnswers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlagged",
                table: "ToeicAttemptAnswers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveTestId",
                table: "ToeicAttempts",
                type: "char(36)",
                nullable: true,
                computedColumnSql: "CASE WHEN `Status` = 'InProgress' THEN `TestId` ELSE NULL END",
                stored: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttempts_Status_Mode_Deadline",
                table: "ToeicAttempts",
                columns: new[] { "Status", "Mode", "Deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttempts_UserId_ActiveTestId",
                table: "ToeicAttempts",
                columns: new[] { "UserId", "ActiveTestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttempts_UserId_StartTime",
                table: "ToeicAttempts",
                columns: new[] { "UserId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ToeicAttempts_Status_Mode_Deadline",
                table: "ToeicAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ToeicAttempts_UserId_ActiveTestId",
                table: "ToeicAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ToeicAttempts_UserId_StartTime",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "ActiveTestId",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "ListeningCorrect",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "ReadingCorrect",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "SelectedParts",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "TimeLimitSeconds",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "CheckedAt",
                table: "ToeicAttemptAnswers");

            migrationBuilder.DropColumn(
                name: "IsFlagged",
                table: "ToeicAttemptAnswers");
        }
    }
}
