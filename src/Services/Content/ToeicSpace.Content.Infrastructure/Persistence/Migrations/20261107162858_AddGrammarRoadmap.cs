using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGrammarRoadmap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GrammarAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Topic = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Stage = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Block = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Answers = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Results = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FixedQuestionIds = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarAttempts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GrammarComments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Target = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AuthorName = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    ReplyCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarComments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GrammarDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Key = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImportedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarDocuments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GrammarLearnerStates",
                columns: table => new
                {
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PositionTopic = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PositionPart = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PositionAnchor = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DarkMode = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    RoadmapWidth = table.Column<int>(type: "int", nullable: true),
                    RoadmapCollapsed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    KnownWords = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarLearnerStates", x => x.LearnerId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GrammarLessonProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Topic = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TheoryConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VocabConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    QuickCheckBest = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MiniTestBest = table.Column<int>(type: "int", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarLessonProgress", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GrammarQuestionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QuestionId = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Topic = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Block = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnitCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrapCode = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstCorrect = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    LastCorrect = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Fixed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Revealed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WrongCount = table.Column<int>(type: "int", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarQuestionResults", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GrammarCommentLikes",
                columns: table => new
                {
                    CommentId = table.Column<long>(type: "bigint", nullable: false),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarCommentLikes", x => new { x.CommentId, x.LearnerId });
                    table.ForeignKey(
                        name: "FK_GrammarCommentLikes_GrammarComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "GrammarComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarAttempts_LearnerId_Stage",
                table: "GrammarAttempts",
                columns: new[] { "LearnerId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarComments_LearnerId_Id",
                table: "GrammarComments",
                columns: new[] { "LearnerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarComments_Target_ParentId_Id",
                table: "GrammarComments",
                columns: new[] { "Target", "ParentId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarDocuments_Kind_Key",
                table: "GrammarDocuments",
                columns: new[] { "Kind", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarLessonProgress_LearnerId_Topic",
                table: "GrammarLessonProgress",
                columns: new[] { "LearnerId", "Topic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarQuestionResults_LearnerId_LastCorrect_WrongCount",
                table: "GrammarQuestionResults",
                columns: new[] { "LearnerId", "LastCorrect", "WrongCount" });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarQuestionResults_LearnerId_QuestionId",
                table: "GrammarQuestionResults",
                columns: new[] { "LearnerId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarQuestionResults_LearnerId_TrapCode_WrongCount",
                table: "GrammarQuestionResults",
                columns: new[] { "LearnerId", "TrapCode", "WrongCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrammarAttempts");

            migrationBuilder.DropTable(
                name: "GrammarCommentLikes");

            migrationBuilder.DropTable(
                name: "GrammarDocuments");

            migrationBuilder.DropTable(
                name: "GrammarLearnerStates");

            migrationBuilder.DropTable(
                name: "GrammarLessonProgress");

            migrationBuilder.DropTable(
                name: "GrammarQuestionResults");

            migrationBuilder.DropTable(
                name: "GrammarComments");
        }
    }
}
