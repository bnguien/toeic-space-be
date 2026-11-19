using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Assessment.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TestId",
                table: "ToeicAttempts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "ToeicAttempts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PracticeSetId",
                table: "ToeicAttempts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "LastMutationId",
                table: "ToeicAttemptAnswers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "ToeicAttemptAnswers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "SnapshotQuestionId",
                table: "ToeicAttemptAnswers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ActivePracticeSetId",
                table: "ToeicAttempts",
                type: "char(36)",
                nullable: true,
                computedColumnSql: "CASE WHEN `Status` = 'InProgress' THEN `PracticeSetId` ELSE NULL END",
                stored: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "PracticeAttemptStates",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PracticeSetId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Generation = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeAttemptStates", x => new { x.UserId, x.PracticeSetId });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ToeicAttemptQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AttemptId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceQuestionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToeicAttemptQuestions", x => x.Id);
                    table.UniqueConstraint("AK_ToeicAttemptQuestions_AttemptId_Id", x => new { x.AttemptId, x.Id });
                    table.ForeignKey(
                        name: "FK_ToeicAttemptQuestions_ToeicAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "ToeicAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ToeicAttemptSnapshots",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToeicAttemptSnapshots", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_ToeicAttemptSnapshots_ToeicAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "ToeicAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttempts_PracticeSetId",
                table: "ToeicAttempts",
                column: "PracticeSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttempts_Status_LastActivityAt_PracticeSetId",
                table: "ToeicAttempts",
                columns: new[] { "Status", "LastActivityAt", "PracticeSetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttempts_UserId_ActivePracticeSetId",
                table: "ToeicAttempts",
                columns: new[] { "UserId", "ActivePracticeSetId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Attempt_Source",
                table: "ToeicAttempts",
                sql: "(`TestId` IS NULL) <> (`PracticeSetId` IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttemptAnswers_AttemptId_SnapshotQuestionId",
                table: "ToeicAttemptAnswers",
                columns: new[] { "AttemptId", "SnapshotQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToeicAttemptQuestions_AttemptId_SourceQuestionId",
                table: "ToeicAttemptQuestions",
                columns: new[] { "AttemptId", "SourceQuestionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ToeicAttemptAnswers_ToeicAttemptQuestions_AttemptId_Snapshot~",
                table: "ToeicAttemptAnswers",
                columns: new[] { "AttemptId", "SnapshotQuestionId" },
                principalTable: "ToeicAttemptQuestions",
                principalColumns: new[] { "AttemptId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ToeicAttempts_ToeicPracticeSets_PracticeSetId",
                table: "ToeicAttempts",
                column: "PracticeSetId",
                principalTable: "ToeicPracticeSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse a destructive downgrade while new practice history exists. MySQL validates this
            // before any snapshot tables/columns can be dropped; no fabricated TestId is introduced.
            migrationBuilder.AddCheckConstraint("CK_Downgrade_NoPractice", "ToeicAttempts", "`PracticeSetId` IS NULL");
            migrationBuilder.DropCheckConstraint("CK_Downgrade_NoPractice", "ToeicAttempts");
            migrationBuilder.DropForeignKey(
                name: "FK_ToeicAttemptAnswers_ToeicAttemptQuestions_AttemptId_Snapshot~",
                table: "ToeicAttemptAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ToeicAttempts_ToeicPracticeSets_PracticeSetId",
                table: "ToeicAttempts");

            migrationBuilder.DropTable(
                name: "PracticeAttemptStates");

            migrationBuilder.DropTable(
                name: "ToeicAttemptQuestions");

            migrationBuilder.DropTable(
                name: "ToeicAttemptSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ToeicAttempts_PracticeSetId",
                table: "ToeicAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ToeicAttempts_Status_LastActivityAt_PracticeSetId",
                table: "ToeicAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ToeicAttempts_UserId_ActivePracticeSetId",
                table: "ToeicAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Attempt_Source",
                table: "ToeicAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ToeicAttemptAnswers_AttemptId_SnapshotQuestionId",
                table: "ToeicAttemptAnswers");

            migrationBuilder.DropColumn(
                name: "ActivePracticeSetId",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "PracticeSetId",
                table: "ToeicAttempts");

            migrationBuilder.DropColumn(
                name: "LastMutationId",
                table: "ToeicAttemptAnswers");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ToeicAttemptAnswers");

            migrationBuilder.DropColumn(
                name: "SnapshotQuestionId",
                table: "ToeicAttemptAnswers");

            migrationBuilder.AlterColumn<Guid>(
                name: "TestId",
                table: "ToeicAttempts",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }
    }
}
