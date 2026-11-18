using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Content.Application.DTOs.Analytics;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Tests.Vocabulary;

namespace ToeicSpace.Content.Tests.Analytics;

/// <summary>
/// SQLite in-memory Content database for analytics tests. The clock is 2026-11-14 12:00 UTC, so the default range
/// is 2026-08-17 .. 2026-11-14.
/// </summary>
internal sealed class AnalyticsTestDatabase : IAsyncDisposable
{
    public static readonly DateTime Recent = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<ContentDbContext> _requestContexts = [];
    private int _sequence;

    private AnalyticsTestDatabase()
    {
        Context = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);
    }

    public ContentDbContext Context { get; }

    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));

    public static async Task<AnalyticsTestDatabase> CreateAsync()
    {
        var database = new AnalyticsTestDatabase();
        await database._connection.OpenAsync();
        await database.Context.Database.EnsureCreatedAsync();
        return database;
    }

    public static List<Guid> Learners(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    public Task<AnalyticsResultDto> QueryAsync(QueryAnalyticsQuery query)
    {
        // A fresh context per request, like one HTTP scope.
        var context = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);
        _requestContexts.Add(context);
        return new QueryAnalyticsHandler(new ContentAnalyticsRepository(context), Clock).Handle(query, CancellationToken.None);
    }

    public T Add<T>(T entity) where T : class
    {
        Context.Add(entity);
        Context.SaveChanges();
        return entity;
    }

    public Course AddCourse(
        CourseStatus status = CourseStatus.Published,
        CourseLevel level = CourseLevel.Beginner,
        DateTime? createdAt = null,
        string? code = null)
    {
        var number = Next();
        return Add(new Course
        {
            Code = code ?? $"COURSE-{number:000}",
            Title = $"Course {number}",
            Slug = $"course-{number}",
            ExamType = CourseExamType.ListeningReading,
            Level = level,
            Status = status,
            CreatedAt = createdAt ?? Recent,
            UpdatedAt = createdAt ?? Recent
        });
    }

    public Lesson AddLesson(Course course, LessonStatus status = LessonStatus.Published)
    {
        var module = Context.CourseModules.FirstOrDefault(x => x.CourseId == course.Id)
            ?? Add(new CourseModule { CourseId = course.Id, Title = "Module", SortOrder = 1, CreatedAt = Recent, UpdatedAt = Recent });
        return Add(new Lesson
        {
            ModuleId = module.Id,
            Title = $"Lesson {Next()}",
            SortOrder = Next(),
            Status = status,
            CreatedAt = Recent,
            UpdatedAt = Recent
        });
    }

    public (VocabularyStage Stage, List<VocabularyWord> Words) AddStage(string slug, int order, int words)
    {
        var stage = Add(new VocabularyStage { Slug = slug, Title = slug, Order = order, CreatedAt = Recent, UpdatedAt = Recent });
        var set = Add(new VocabularySet { StageId = stage.Id, Slug = $"{slug}-set", Title = "Set", Order = 1, CreatedAt = Recent, UpdatedAt = Recent });
        var list = new List<VocabularyWord>();

        for (var index = 0; index < words; index++)
        {
            var word = AddWord();
            Add(new VocabularySetWord { SetId = set.Id, WordId = word.Id, Order = index + 1 });
            list.Add(word);
        }

        return (stage, list);
    }

    public VocabularyWord AddWord(Guid? owner = null)
    {
        var headword = $"word{Next()}";
        return Add(new VocabularyWord
        {
            OwnerKey = owner ?? Guid.Empty,
            Headword = headword,
            NormalizedHeadword = headword,
            PartOfSpeech = PartOfSpeech.Noun,
            Phonetic = "/w/",
            MeaningVi = "từ",
            CreatedAt = Recent,
            UpdatedAt = Recent
        });
    }

    public WordReviewLog AddReview(
        Guid learner,
        Guid wordId,
        ReviewGrade grade,
        int intervalBefore = 0,
        int intervalAfter = 1,
        DateTime? at = null)
        => Add(new WordReviewLog
        {
            LearnerId = learner,
            WordId = wordId,
            Mode = StudyMode.Flashcard,
            Grade = grade,
            ClientEventId = Guid.NewGuid(),
            ReviewedAt = at ?? Recent,
            LocalDay = DateOnly.FromDateTime(at ?? Recent),
            IntervalBefore = intervalBefore,
            IntervalAfter = intervalAfter,
            EaseFactorBefore = 2.5,
            EaseFactorAfter = 2.5,
            ScheduleChanged = true
        });

    public StudySession AddSession(Guid learner, StudyMode mode, DateTime start, TimeSpan length, bool completed = true)
        => Add(new StudySession
        {
            LearnerId = learner,
            Mode = mode,
            SourceKind = StudySourceKind.Set,
            Status = completed ? StudySessionStatus.Completed : StudySessionStatus.Active,
            StartedAt = start,
            UpdatedAt = start + length,
            CompletedAt = completed ? start + length : null
        });

    public DictationCourse AddDictationCourse(string code)
    {
        var course = Add(new DictationCourse { Code = code, Pack = "toeic-250", Level = 1, Order = Next(), Title = code, CreatedAt = Recent, UpdatedAt = Recent });
        return course;
    }

    public DictationItem AddDictationItem(DictationCourse course)
        => Add(new DictationItem
        {
            CourseId = course.Id,
            SourceCode = $"{course.Code}-{Next()}",
            Order = Next(),
            Level = 1,
            Text = "The cat sat.",
            CreatedAt = Recent,
            UpdatedAt = Recent
        });

    public DictationProgress AddDictationProgress(Guid learner, DictationItem item, bool passed, DateTime? at = null)
        => Add(new DictationProgress
        {
            LearnerId = learner,
            ItemId = item.Id,
            Passed = passed,
            AttemptCount = 1,
            LastAttemptAt = at ?? Recent,
            CreatedAt = at ?? Recent,
            UpdatedAt = at ?? Recent
        });

    public NewsArticle AddArticle(string slug)
        => Add(new NewsArticle
        {
            Slug = slug,
            SourceName = "VOA",
            SourceUrl = $"https://example.org/{slug}",
            License = "public-domain",
            Attribution = "VOA",
            TitleEn = slug,
            Stage = "c1",
            Topic = "world",
            Status = NewsArticleStatus.Published,
            CreatedAt = Recent,
            UpdatedAt = Recent
        });

    private int Next() => Interlocked.Increment(ref _sequence);

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _requestContexts)
        {
            await context.DisposeAsync();
        }

        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
