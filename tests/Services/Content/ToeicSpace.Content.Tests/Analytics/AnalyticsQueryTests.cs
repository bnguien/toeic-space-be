using System.Text.Json;
using ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Content.Application.DTOs.Analytics;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Analytics;

/// <summary>Metric computations on SQLite, small-group suppression and the shape of the rows.</summary>
public sealed class AnalyticsQueryTests
{
    private static readonly DateTime Recent = AnalyticsTestDatabase.Recent;

    [Fact]
    public async Task StudySessions_GroupsWithFewerThanFiveLearners_AreSuppressed()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();

        foreach (var learner in AnalyticsTestDatabase.Learners(5))
        {
            database.AddSession(learner, StudyMode.Flashcard, Recent, TimeSpan.FromMinutes(10));
            database.AddSession(learner, StudyMode.Flashcard, Recent.AddHours(1), TimeSpan.FromMinutes(10));
        }

        foreach (var learner in AnalyticsTestDatabase.Learners(2))
        {
            database.AddSession(learner, StudyMode.Quiz, Recent, TimeSpan.FromMinutes(10));
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("study_sessions", By: "kind"));

        Assert.Equal(1, result.Suppressed);
        Assert.Equal(["Flashcard", "Quiz"], result.Rows.Select(row => row["kind"]));
        Assert.Equal(10.0, Cell(result, 0, "value"));
        Assert.Equal(10, Cell(result, 0, "n"));
        Assert.Null(Cell(result, 1, "value"));
        Assert.Equal(0, Cell(result, 1, "n"));
    }

    [Fact]
    public async Task StudyTime_SumsMinutesAndCapsForgottenSessions()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var learners = AnalyticsTestDatabase.Learners(5);

        foreach (var learner in learners.Skip(1))
        {
            database.AddSession(learner, StudyMode.Quiz, Recent, TimeSpan.FromMinutes(30));
        }

        // Left open for ten hours: counts as the 180-minute cap.
        database.AddSession(learners[0], StudyMode.Quiz, Recent, TimeSpan.FromHours(10), completed: false);

        // Outside the default range (before 2026-08-17).
        database.AddSession(learners[0], StudyMode.Quiz, new DateTime(2026, 8, 16, 23, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        var result = await database.QueryAsync(new QueryAnalyticsQuery("study_time"));

        Assert.Equal("minutes", result.Unit);
        Assert.Equal("learners", result.CountUnit);
        Assert.Equal(300.0, Cell(result, 0, "value"));
        Assert.Equal(5, Cell(result, 0, "n"));
        Assert.Equal(new DateOnly(2026, 8, 17), result.From);
        Assert.Equal(new DateOnly(2026, 11, 14), result.To);
    }

    [Fact]
    public async Task ActiveLearners_CountsEachLearnerOnceAcrossActivities()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var learners = AnalyticsTestDatabase.Learners(5);
        var word = database.AddWord();
        var article = database.AddArticle("rain");

        database.AddReview(learners[0], word.Id, ReviewGrade.Good);
        database.AddReview(learners[1], word.Id, ReviewGrade.Good);
        database.AddSession(learners[1], StudyMode.Reel, Recent, TimeSpan.FromMinutes(5));
        database.Add(NewsReading.Open(learners[2], article.Id, Recent));
        database.Add(new GrammarAttempt { LearnerId = learners[3], Topic = "04", Block = "mt", CreatedAt = Recent });
        database.Add(new VideoWatch { LearnerId = learners[4], VideoId = "abc123", LastOpenedAt = Recent, CreatedAt = Recent, UpdatedAt = Recent });

        var result = await database.QueryAsync(new QueryAnalyticsQuery("active_learners", By: "week"));

        Assert.Equal("2026-11-09", Assert.Single(result.Rows)["week"]);
        Assert.Equal(5.0, Cell(result, 0, "value"));
    }

    [Fact]
    public async Task VocabularyReviews_AccuracyRetentionAndStage()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var (stage, words) = database.AddStage("toeic-450", 1, 2);

        foreach (var learner in AnalyticsTestDatabase.Learners(5))
        {
            var custom = database.AddWord(owner: learner);
            database.AddReview(learner, words[0].Id, ReviewGrade.Good, intervalBefore: 0);
            database.AddReview(learner, words[1].Id, ReviewGrade.Forget, intervalBefore: 6);
            database.AddReview(learner, words[0].Id, ReviewGrade.Easy, intervalBefore: 1, intervalAfter: 6, at: Recent.AddDays(1));
            database.AddReview(learner, custom.Id, ReviewGrade.Seen);
        }

        Assert.Equal(20.0, Cell(await database.QueryAsync(new QueryAnalyticsQuery("vocabulary_reviews")), 0, "value"));

        var accuracy = await database.QueryAsync(new QueryAnalyticsQuery("vocabulary_review_accuracy"));
        Assert.Equal(0.6667, Cell(accuracy, 0, "value"));
        Assert.Equal(15, Cell(accuracy, 0, "n"));

        var retention = await database.QueryAsync(new QueryAnalyticsQuery("vocabulary_review_retention"));
        Assert.Equal(0.5, Cell(retention, 0, "value"));
        Assert.Equal(10, Cell(retention, 0, "n"));

        var byStage = await database.QueryAsync(new QueryAnalyticsQuery("vocabulary_reviews", By: "stage"));
        Assert.Equal([stage.Id.ToString(), null], byStage.Rows.Select(row => row["stage"]));
        Assert.Equal(15.0, Cell(byStage, 0, "value"));
        Assert.Equal(5.0, Cell(byStage, 1, "value"));
        Assert.Equal("toeic-450", byStage.Labels![stage.Id.ToString()]);
    }

    [Fact]
    public async Task WordsLearned_CountsEachLearnerWordOnceWhenItReachesTheMasteredInterval()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var (_, words) = database.AddStage("toeic-650", 1, 1);

        foreach (var learner in AnalyticsTestDatabase.Learners(5))
        {
            database.AddReview(learner, words[0].Id, ReviewGrade.Good, intervalBefore: 6, intervalAfter: 15);
            database.AddReview(learner, words[0].Id, ReviewGrade.Good, intervalBefore: 15, intervalAfter: 38, at: Recent.AddDays(1));
            database.AddReview(learner, words[0].Id, ReviewGrade.Good, intervalBefore: 1, intervalAfter: 21, at: Recent.AddDays(2));
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("vocabulary_words_learned"));

        Assert.Equal(5.0, Cell(result, 0, "value"));
        Assert.Equal(5, Cell(result, 0, "n"));
    }

    [Fact]
    public async Task DictationCompletion_ByCourseWithCodesAndCourseFilter()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var first = database.AddDictationCourse("sc_crs_t250_001");
        var second = database.AddDictationCourse("sc_crs_t250_002");
        var items = new[] { database.AddDictationItem(first), database.AddDictationItem(first), database.AddDictationItem(second) };

        foreach (var learner in AnalyticsTestDatabase.Learners(5))
        {
            database.AddDictationProgress(learner, items[0], passed: true);
            database.AddDictationProgress(learner, items[1], passed: false);
            database.AddDictationProgress(learner, items[2], passed: true);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("dictation_completion", By: "course"));

        Assert.Equal([first.Id.ToString(), second.Id.ToString()], result.Rows.Select(row => row["course"]));
        Assert.Equal(0.5, Cell(result, 0, "value"));
        Assert.Equal(10, Cell(result, 0, "n"));
        Assert.Equal(1.0, Cell(result, 1, "value"));
        Assert.Equal("sc_crs_t250_001", result.Labels![first.Id.ToString()]);

        var filtered = await database.QueryAsync(new QueryAnalyticsQuery("dictation_attempts", CourseId: second.Id.ToString()));
        Assert.Equal(5.0, Cell(filtered, 0, "value"));
    }

    [Fact]
    public async Task Grammar_AttemptsAccuracyAndCompletionByDocument()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();

        foreach (var learner in AnalyticsTestDatabase.Learners(5))
        {
            database.Add(new GrammarAttempt { LearnerId = learner, Topic = "04", Block = "mt", Score = 3, Total = 5, CreatedAt = Recent });
            database.Add(new GrammarAttempt { LearnerId = learner, Stage = "01", Block = "stage", Score = 8, Total = 10, CreatedAt = Recent });
            database.Add(Result(learner, "q1", firstCorrect: true));
            database.Add(Result(learner, "q2", firstCorrect: false));
            database.Add(Result(learner, "q3", firstCorrect: true));
            database.Add(Result(learner, "q4", firstCorrect: null));
            database.Add(new GrammarLessonProgress { LearnerId = learner, Topic = "04", CompletedAt = Recent, UpdatedAt = Recent });
            database.Add(new GrammarLessonProgress { LearnerId = learner, Topic = "05", UpdatedAt = Recent });
        }

        var attempts = await database.QueryAsync(new QueryAnalyticsQuery("grammar_attempts", By: "document"));
        Assert.Equal(["stage-01", "topic-04"], attempts.Rows.Select(row => row["document"]));
        Assert.Equal(5.0, Cell(attempts, 0, "value"));

        var accuracy = await database.QueryAsync(new QueryAnalyticsQuery("grammar_accuracy"));
        Assert.Equal(0.6667, Cell(accuracy, 0, "value"));
        Assert.Equal(15, Cell(accuracy, 0, "n"));

        var completion = await database.QueryAsync(new QueryAnalyticsQuery("grammar_lesson_completion", By: "month"));
        Assert.Equal("2026-11", Cell(completion, 0, "month"));
        Assert.Equal(0.5, Cell(completion, 0, "value"));
        Assert.Equal(10, Cell(completion, 0, "n"));

        static GrammarQuestionResult Result(Guid learner, string question, bool? firstCorrect) => new()
        {
            LearnerId = learner,
            QuestionId = question,
            Topic = "04",
            Block = "qc-u1",
            FirstCorrect = firstCorrect,
            Revealed = firstCorrect is null,
            AnsweredAt = firstCorrect is null ? null : Recent,
            UpdatedAt = Recent
        };
    }

    [Fact]
    public async Task VideoWatchTime_SumsTheFurthestPointWatched()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        database.Add(new VideoCatalogItem { VideoId = "vid00000001", Title = "Coffee talk", CreatedAt = Recent, UpdatedAt = Recent });

        foreach (var learner in AnalyticsTestDatabase.Learners(5))
        {
            database.Add(new VideoWatch
            {
                LearnerId = learner, VideoId = "vid00000001", Duration = 600, WatchedPercent = 50,
                ProgressAt = Recent, LastOpenedAt = Recent, CreatedAt = Recent.AddDays(-200), UpdatedAt = Recent
            });
        }

        var time = await database.QueryAsync(new QueryAnalyticsQuery("video_watch_time", By: "video"));
        Assert.Equal(1500.0, Cell(time, 0, "value"));
        Assert.Equal("Coffee talk", time.Labels!["vid00000001"]);

        // First opened outside the range: no new watch in the range.
        var watches = await database.QueryAsync(new QueryAnalyticsQuery("video_watches"));
        Assert.Null(Cell(watches, 0, "value"));
        Assert.Equal(1, watches.Suppressed);
    }

    [Fact]
    public async Task NewsReadCompletion_ByArticleWithSlugs()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var article = database.AddArticle("city-bikes");
        var learners = AnalyticsTestDatabase.Learners(5);

        foreach (var learner in learners)
        {
            var reading = NewsReading.Open(learner, article.Id, Recent);
            if (learner != learners[0])
            {
                reading.Complete(Recent.AddMinutes(5));
            }

            database.Add(reading);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("news_read_completion", By: "article"));

        Assert.Equal(article.Id.ToString(), Cell(result, 0, "article"));
        Assert.Equal(0.8, Cell(result, 0, "value"));
        Assert.Equal(5, Cell(result, 0, "n"));
        Assert.Equal("city-bikes", result.Labels![article.Id.ToString()]);
    }

    [Fact]
    public async Task CourseAndLessonCounts_AreNotSuppressed()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var published = database.AddCourse(code: "LR-450");
        database.AddCourse(CourseStatus.Draft, CourseLevel.Advanced);
        database.AddLesson(published);
        database.AddLesson(published);
        database.AddLesson(published, LessonStatus.Draft);

        var courses = await database.QueryAsync(new QueryAnalyticsQuery("course_count", By: "status"));
        Assert.Equal(0, courses.Suppressed);
        Assert.Null(courses.From);
        Assert.Equal(["Draft", "Published"], courses.Rows.Select(row => row["status"]));
        Assert.Equal([1.0, 1.0], courses.Rows.Select(row => row["value"]));

        var advanced = await database.QueryAsync(new QueryAnalyticsQuery("course_count", Level: "advanced"));
        Assert.Equal(1.0, Cell(advanced, 0, "value"));

        var lessons = await database.QueryAsync(new QueryAnalyticsQuery("lesson_count", Status: "published"));
        Assert.Equal(["course"], lessons.By);
        Assert.Equal(published.Id.ToString(), Cell(lessons, 0, "course"));
        Assert.Equal(2.0, Cell(lessons, 0, "value"));
        Assert.Equal("LR-450", lessons.Labels![published.Id.ToString()]);
    }

    [Fact]
    public async Task Limit_TruncatesTheRows()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();

        for (var day = 0; day < 5; day++)
        {
            database.AddCourse(createdAt: Recent.AddDays(-day));
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("course_count", By: "day", Limit: 3));

        Assert.Equal(3, result.Rows.Count);
        Assert.True(result.Truncated);
        Assert.Equal("2026-11-06", Cell(result, 0, "day"));
    }

    [Fact]
    public async Task Json_KeepsNullValuesAndNeverContainsLearnerIds()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();
        var article = database.AddArticle("night-market");
        var learners = AnalyticsTestDatabase.Learners(3);

        foreach (var learner in learners)
        {
            database.Add(NewsReading.Open(learner, article.Id, Recent));
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("news_reads", By: "day"));
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"value\":null", json);
        Assert.Contains("\"day\":\"2026-11-10\"", json);
        Assert.All(learners, learner => Assert.DoesNotContain(learner.ToString(), json));
    }

    [Fact]
    public async Task InvalidQuery_IsRejectedByTheHandlerWithEveryError()
    {
        await using var database = await AnalyticsTestDatabase.CreateAsync();

        var error = await Assert.ThrowsAsync<ContentRuleException>(() =>
            database.QueryAsync(new QueryAnalyticsQuery("news_reads", By: "stage", From: "yesterday")));

        Assert.Equal("validation_error", error.Code);
        Assert.Equal(400, error.StatusCode);
        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(error.Extensions!["errors"]);
        Assert.Equal(["by", "from"], errors.Keys.Order());
    }

    private static object? Cell(AnalyticsResultDto result, int row, string column) => result.Rows[row][column];
}
