using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToeicSpace.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearnerVocabularyPreferences",
                columns: table => new
                {
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FontScale = table.Column<double>(type: "double", nullable: false),
                    SidebarCollapsed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SidebarWidth = table.Column<int>(type: "int", nullable: true),
                    Snowfall = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AutoPlayAudio = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultMode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DailyGoal = table.Column<int>(type: "int", nullable: false),
                    LastStageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastView = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastFolderId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastFolderPickerId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerVocabularyPreferences", x => x.LearnerId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReelBackgrounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Provider = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VideoUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PosterUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PageUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Author = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Tags = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastSeenAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MissCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReelBackgrounds", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Mode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceKind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SourceTitle = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Queue = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Requeued = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Results = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    ReviewCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActiveSlot = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VocabularyFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedName = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyFolders", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VocabularyStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Slug = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ToeicTarget = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyStages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VocabularyWords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OwnerKey = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Headword = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedHeadword = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PartOfSpeech = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phonetic = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MeaningVi = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefinitionEn = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Example = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExampleVi = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cefr = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Synonyms = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyWords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WordGlossCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NormalizedText = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Headword = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phonetic = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PartOfSpeech = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MeaningVi = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefinitionEn = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Example = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExampleVi = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cefr = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhraseType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Usage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Explanation = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LiteralMeaning = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sources = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordGlossCache", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VocabularySets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    StageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Slug = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Topic = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularySets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularySets_VocabularyStages_StageId",
                        column: x => x.StageId,
                        principalTable: "VocabularyStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LearnerWordProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EaseFactor = table.Column<double>(type: "double", nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: false),
                    Repetitions = table.Column<int>(type: "int", nullable: false),
                    Lapses = table.Column<int>(type: "int", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastGradeDay = table.Column<DateOnly>(type: "date", nullable: true),
                    LastGrade = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerWordProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerWordProgress_VocabularyWords_WordId",
                        column: x => x.WordId,
                        principalTable: "VocabularyWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SavedWords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FolderId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Note = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SavedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedWords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedWords_VocabularyFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "VocabularyFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SavedWords_VocabularyWords_WordId",
                        column: x => x.WordId,
                        principalTable: "VocabularyWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WordReviewLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LearnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Mode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Grade = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClientEventId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LocalDay = table.Column<DateOnly>(type: "date", nullable: false),
                    IntervalBefore = table.Column<int>(type: "int", nullable: false),
                    IntervalAfter = table.Column<int>(type: "int", nullable: false),
                    EaseFactorBefore = table.Column<double>(type: "double", nullable: false),
                    EaseFactorAfter = table.Column<double>(type: "double", nullable: false),
                    ScheduleChanged = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordReviewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordReviewLogs_VocabularyWords_WordId",
                        column: x => x.WordId,
                        principalTable: "VocabularyWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VocabularySetWords",
                columns: table => new
                {
                    SetId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularySetWords", x => new { x.SetId, x.WordId });
                    table.ForeignKey(
                        name: "FK_VocabularySetWords_VocabularySets_SetId",
                        column: x => x.SetId,
                        principalTable: "VocabularySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VocabularySetWords_VocabularyWords_WordId",
                        column: x => x.WordId,
                        principalTable: "VocabularyWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerWordProgress_LearnerId_DueOn",
                table: "LearnerWordProgress",
                columns: new[] { "LearnerId", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerWordProgress_LearnerId_WordId",
                table: "LearnerWordProgress",
                columns: new[] { "LearnerId", "WordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerWordProgress_WordId",
                table: "LearnerWordProgress",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReelBackgrounds_IsActive",
                table: "ReelBackgrounds",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReelBackgrounds_Provider_ProviderId",
                table: "ReelBackgrounds",
                columns: new[] { "Provider", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedWords_FolderId",
                table: "SavedWords",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedWords_LearnerId_FolderId_SavedAt",
                table: "SavedWords",
                columns: new[] { "LearnerId", "FolderId", "SavedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedWords_LearnerId_WordId",
                table: "SavedWords",
                columns: new[] { "LearnerId", "WordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedWords_WordId",
                table: "SavedWords",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_LearnerId_ActiveSlot",
                table: "StudySessions",
                columns: new[] { "LearnerId", "ActiveSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_LearnerId_Status_UpdatedAt",
                table: "StudySessions",
                columns: new[] { "LearnerId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyFolders_LearnerId_NormalizedName",
                table: "VocabularyFolders",
                columns: new[] { "LearnerId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularySets_StageId_Order",
                table: "VocabularySets",
                columns: new[] { "StageId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularySets_StageId_Slug",
                table: "VocabularySets",
                columns: new[] { "StageId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularySetWords_SetId_Order",
                table: "VocabularySetWords",
                columns: new[] { "SetId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularySetWords_WordId",
                table: "VocabularySetWords",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyStages_Order",
                table: "VocabularyStages",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyStages_Slug",
                table: "VocabularyStages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyWords_OwnerKey_NormalizedHeadword_PartOfSpeech",
                table: "VocabularyWords",
                columns: new[] { "OwnerKey", "NormalizedHeadword", "PartOfSpeech" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WordGlossCache_ExpiresAt",
                table: "WordGlossCache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WordGlossCache_NormalizedText_Kind",
                table: "WordGlossCache",
                columns: new[] { "NormalizedText", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WordReviewLogs_LearnerId_ClientEventId",
                table: "WordReviewLogs",
                columns: new[] { "LearnerId", "ClientEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WordReviewLogs_LearnerId_LocalDay",
                table: "WordReviewLogs",
                columns: new[] { "LearnerId", "LocalDay" });

            migrationBuilder.CreateIndex(
                name: "IX_WordReviewLogs_WordId",
                table: "WordReviewLogs",
                column: "WordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearnerVocabularyPreferences");

            migrationBuilder.DropTable(
                name: "LearnerWordProgress");

            migrationBuilder.DropTable(
                name: "ReelBackgrounds");

            migrationBuilder.DropTable(
                name: "SavedWords");

            migrationBuilder.DropTable(
                name: "StudySessions");

            migrationBuilder.DropTable(
                name: "VocabularySetWords");

            migrationBuilder.DropTable(
                name: "WordGlossCache");

            migrationBuilder.DropTable(
                name: "WordReviewLogs");

            migrationBuilder.DropTable(
                name: "VocabularyFolders");

            migrationBuilder.DropTable(
                name: "VocabularySets");

            migrationBuilder.DropTable(
                name: "VocabularyWords");

            migrationBuilder.DropTable(
                name: "VocabularyStages");
        }
    }
}
