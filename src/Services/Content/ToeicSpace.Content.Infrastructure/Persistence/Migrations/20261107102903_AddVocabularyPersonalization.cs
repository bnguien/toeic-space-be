using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyPersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "SavedWords",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(300)",
                oldMaxLength: 300,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MyExample",
                table: "SavedWords",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MyExampleVi",
                table: "SavedWords",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MyMeaningVi",
                table: "SavedWords",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MyPartOfSpeech",
                table: "SavedWords",
                type: "varchar(24)",
                maxLength: 24,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MyPhonetic",
                table: "SavedWords",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PersonalizedAt",
                table: "SavedWords",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMistakeAt",
                table: "LearnerWordProgress",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MistakeCount",
                table: "LearnerWordProgress",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "MistakePending",
                table: "LearnerWordProgress",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StudySettings",
                table: "LearnerVocabularyPreferences",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerWordProgress_LearnerId_MistakePending_LastMistakeAt",
                table: "LearnerWordProgress",
                columns: new[] { "LearnerId", "MistakePending", "LastMistakeAt" });

            // Backfill the mistake flags from the review history: count = Forget reviews, last = latest Forget,
            // pending = no Good/Easy after that Forget (same rule as ReviewRecorder from now on).
            migrationBuilder.Sql(
                """
                UPDATE `LearnerWordProgress` AS p
                JOIN (
                    SELECT `LearnerId`, `WordId`, COUNT(*) AS `Mistakes`, MAX(`ReviewedAt`) AS `LastAt`
                    FROM `WordReviewLogs`
                    WHERE `Grade` = 'Forget'
                    GROUP BY `LearnerId`, `WordId`
                ) AS f ON f.`LearnerId` = p.`LearnerId` AND f.`WordId` = p.`WordId`
                SET p.`MistakeCount` = f.`Mistakes`,
                    p.`LastMistakeAt` = f.`LastAt`,
                    p.`MistakePending` = NOT EXISTS (
                        SELECT 1 FROM `WordReviewLogs` AS g
                        WHERE g.`LearnerId` = p.`LearnerId` AND g.`WordId` = p.`WordId`
                          AND g.`Grade` IN ('Good', 'Easy') AND g.`ReviewedAt` > f.`LastAt`);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearnerWordProgress_LearnerId_MistakePending_LastMistakeAt",
                table: "LearnerWordProgress");

            migrationBuilder.DropColumn(
                name: "MyExample",
                table: "SavedWords");

            migrationBuilder.DropColumn(
                name: "MyExampleVi",
                table: "SavedWords");

            migrationBuilder.DropColumn(
                name: "MyMeaningVi",
                table: "SavedWords");

            migrationBuilder.DropColumn(
                name: "MyPartOfSpeech",
                table: "SavedWords");

            migrationBuilder.DropColumn(
                name: "MyPhonetic",
                table: "SavedWords");

            migrationBuilder.DropColumn(
                name: "PersonalizedAt",
                table: "SavedWords");

            migrationBuilder.DropColumn(
                name: "LastMistakeAt",
                table: "LearnerWordProgress");

            migrationBuilder.DropColumn(
                name: "MistakeCount",
                table: "LearnerWordProgress");

            migrationBuilder.DropColumn(
                name: "MistakePending",
                table: "LearnerWordProgress");

            migrationBuilder.DropColumn(
                name: "StudySettings",
                table: "LearnerVocabularyPreferences");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "SavedWords",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
