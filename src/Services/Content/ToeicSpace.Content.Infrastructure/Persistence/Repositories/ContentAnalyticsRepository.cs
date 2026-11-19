using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

/// <summary>
/// The fixed EF Core queries behind the analytics metrics. Request values only become typed parameters (dates,
/// IDs, enum values); grouping keys and columns never come from input. Read-only: nothing is tracked.
/// </summary>
public sealed class ContentAnalyticsRepository : IContentAnalyticsRepository
{
    private readonly ContentDbContext _db;

    public ContentAnalyticsRepository(ContentDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<CourseFact>> GetCoursesAsync(
        CourseStatus? status,
        CourseLevel? level,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.Courses.AsNoTracking();

        if (status is { } courseStatus)
        {
            query = query.Where(course => course.Status == courseStatus);
        }

        if (level is { } courseLevel)
        {
            query = query.Where(course => course.Level == courseLevel);
        }

        return LoadAsync(
            query.Select(course => new CourseFact(course.Id, course.Status, course.Level, course.ExamType, course.CreatedAt)),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<LessonPartial>> GetLessonCountsAsync(
        Guid? courseId,
        LessonStatus? status,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.Lessons.AsNoTracking();

        if (courseId is { } course)
        {
            query = query.Where(lesson => lesson.Module.CourseId == course);
        }

        if (status is { } lessonStatus)
        {
            query = query.Where(lesson => lesson.Status == lessonStatus);
        }

        return LoadAsync(
            query
                .GroupBy(lesson => new { lesson.Module.CourseId, lesson.Status })
                .Select(group => new LessonPartial(group.Key.CourseId, group.Key.Status, group.Count())),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<StudySessionFact>> GetStudySessionsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        return LoadAsync(
            _db.StudySessions
                .AsNoTracking()
                .Where(session => session.StartedAt >= from && session.StartedAt < to)
                .Select(session => new StudySessionFact(
                    session.LearnerId,
                    session.Mode,
                    session.SourceKind,
                    session.StartedAt,
                    session.CompletedAt ?? session.UpdatedAt)),
            take,
            cancellationToken);
    }

    /// <summary>
    /// One distinct (learner, day) query per activity table, merged in memory. Tables that keep only the latest
    /// time of an activity (progress rows) contribute that day only.
    /// </summary>
    public async Task<IReadOnlyList<LearnerDay>> GetActiveLearnerDaysAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        var sources = new[]
        {
            _db.StudySessions.Where(x => x.StartedAt >= from && x.StartedAt < to)
                .Select(x => new { x.LearnerId, Day = x.StartedAt.Date }),
            _db.StudySessions.Where(x => x.UpdatedAt >= from && x.UpdatedAt < to)
                .Select(x => new { x.LearnerId, Day = x.UpdatedAt.Date }),
            _db.WordReviewLogs.Where(x => x.ReviewedAt >= from && x.ReviewedAt < to)
                .Select(x => new { x.LearnerId, Day = x.ReviewedAt.Date }),
            _db.SavedWords.Where(x => x.SavedAt >= from && x.SavedAt < to)
                .Select(x => new { x.LearnerId, Day = x.SavedAt.Date }),
            _db.DictationProgress.Where(x => x.LastAttemptAt >= from && x.LastAttemptAt < to)
                .Select(x => new { x.LearnerId, Day = x.LastAttemptAt!.Value.Date }),
            _db.GrammarAttempts.Where(x => x.CreatedAt >= from && x.CreatedAt < to)
                .Select(x => new { x.LearnerId, Day = x.CreatedAt.Date }),
            _db.GrammarQuestionResults.Where(x => x.AnsweredAt >= from && x.AnsweredAt < to)
                .Select(x => new { x.LearnerId, Day = x.AnsweredAt!.Value.Date }),
            _db.GrammarLessonProgress.Where(x => x.UpdatedAt >= from && x.UpdatedAt < to)
                .Select(x => new { x.LearnerId, Day = x.UpdatedAt.Date }),
            _db.VideoWatches.Where(x => x.LastOpenedAt >= from && x.LastOpenedAt < to)
                .Select(x => new { x.LearnerId, Day = x.LastOpenedAt!.Value.Date }),
            _db.VideoWatches.Where(x => x.ProgressAt >= from && x.ProgressAt < to)
                .Select(x => new { x.LearnerId, Day = x.ProgressAt!.Value.Date }),
            _db.NewsReadings.Where(x => x.OpenedAt >= from && x.OpenedAt < to)
                .Select(x => new { x.LearnerId, Day = x.OpenedAt.Date }),
            _db.NewsReadings.Where(x => x.CompletedAt >= from && x.CompletedAt < to)
                .Select(x => new { x.LearnerId, Day = x.CompletedAt!.Value.Date })
        };

        var days = new HashSet<LearnerDay>();

        foreach (var source in sources)
        {
            var rows = await source.AsNoTracking().Distinct().Take(take).ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                days.Add(new LearnerDay(row.LearnerId, row.Day));
            }

            if (days.Count >= take)
            {
                break;
            }
        }

        return [.. days.Take(take)];
    }

    public Task<IReadOnlyList<ReviewPartial>> GetReviewPartialsAsync(
        AnalyticsDateRange range,
        bool byWord,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        var reviews = _db.WordReviewLogs
            .AsNoTracking()
            .Where(log => log.ReviewedAt >= from && log.ReviewedAt < to)
            .Select(log => new
            {
                log.LearnerId,
                Day = log.ReviewedAt.Date,
                log.WordId,
                Graded = log.Grade != ReviewGrade.Seen,
                Correct = log.Grade == ReviewGrade.Good || log.Grade == ReviewGrade.Easy,
                Scheduled = log.IntervalBefore > 0 && log.Grade != ReviewGrade.Seen,
                Recalled = log.IntervalBefore > 0 && log.Grade != ReviewGrade.Seen && log.Grade != ReviewGrade.Forget
            });

        var partials = byWord
            ? reviews
                .GroupBy(review => new { review.LearnerId, review.Day, review.WordId })
                .Select(group => new ReviewPartial(
                    group.Key.LearnerId,
                    group.Key.Day,
                    group.Key.WordId,
                    group.Count(),
                    group.Count(review => review.Graded),
                    group.Count(review => review.Correct),
                    group.Count(review => review.Scheduled),
                    group.Count(review => review.Recalled)))
            : reviews
                .GroupBy(review => new { review.LearnerId, review.Day })
                .Select(group => new ReviewPartial(
                    group.Key.LearnerId,
                    group.Key.Day,
                    null,
                    group.Count(),
                    group.Count(review => review.Graded),
                    group.Count(review => review.Correct),
                    group.Count(review => review.Scheduled),
                    group.Count(review => review.Recalled)));

        return LoadAsync(partials, take, cancellationToken);
    }

    public Task<IReadOnlyList<MasteredWordFact>> GetMasteredWordsAsync(
        AnalyticsDateRange range,
        int masteredIntervalDays,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        return LoadAsync(
            _db.WordReviewLogs
                .AsNoTracking()
                .Where(log => log.ReviewedAt >= from && log.ReviewedAt < to
                    && log.IntervalBefore < masteredIntervalDays && log.IntervalAfter >= masteredIntervalDays)
                .Select(log => new MasteredWordFact(log.LearnerId, log.WordId, log.ReviewedAt)),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<WordStage>> GetWordStagesAsync(
        int take,
        CancellationToken cancellationToken)
        => LoadAsync(
            _db.VocabularySetWords
                .AsNoTracking()
                .Select(membership => new WordStage(
                    membership.WordId,
                    membership.Set!.StageId,
                    membership.Set.Stage!.Order)),
            take,
            cancellationToken);

    public Task<IReadOnlyList<SavedWordPartial>> GetSavedWordPartialsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        return LoadAsync(
            _db.SavedWords
                .AsNoTracking()
                .Where(saved => saved.SavedAt >= from && saved.SavedAt < to)
                .Select(saved => new { saved.LearnerId, Day = saved.SavedAt.Date })
                .GroupBy(saved => new { saved.LearnerId, saved.Day })
                .Select(group => new SavedWordPartial(group.Key.LearnerId, group.Key.Day, group.Count())),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<DictationPartial>> GetDictationPartialsAsync(
        AnalyticsDateRange range,
        int? courseId,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        var query = _db.DictationProgress
            .AsNoTracking()
            .Where(progress => progress.LastAttemptAt >= from && progress.LastAttemptAt < to);

        if (courseId is { } course)
        {
            query = query.Where(progress => progress.Item!.CourseId == course);
        }

        return LoadAsync(
            query
                .Select(progress => new
                {
                    progress.LearnerId,
                    Day = progress.LastAttemptAt!.Value.Date,
                    progress.Item!.CourseId,
                    progress.Passed
                })
                .GroupBy(progress => new { progress.LearnerId, progress.Day, progress.CourseId })
                .Select(group => new DictationPartial(
                    group.Key.LearnerId,
                    group.Key.Day,
                    group.Key.CourseId,
                    group.Count(),
                    group.Count(progress => progress.Passed))),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<GrammarAttemptFact>> GetGrammarAttemptsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        return LoadAsync(
            _db.GrammarAttempts
                .AsNoTracking()
                .Where(attempt => attempt.CreatedAt >= from && attempt.CreatedAt < to)
                .Select(attempt => new GrammarAttemptFact(attempt.LearnerId, attempt.CreatedAt, attempt.Topic, attempt.Stage)),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<GrammarAnswerPartial>> GetGrammarAnswerPartialsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        return LoadAsync(
            _db.GrammarQuestionResults
                .AsNoTracking()
                .Where(result => result.FirstCorrect != null && result.AnsweredAt >= from && result.AnsweredAt < to)
                .Select(result => new
                {
                    result.LearnerId,
                    Day = result.AnsweredAt!.Value.Date,
                    result.Topic,
                    Correct = result.FirstCorrect == true
                })
                .GroupBy(result => new { result.LearnerId, result.Day, result.Topic })
                .Select(group => new GrammarAnswerPartial(
                    group.Key.LearnerId,
                    group.Key.Day,
                    group.Key.Topic,
                    group.Count(),
                    group.Count(result => result.Correct))),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<GrammarLessonFact>> GetGrammarLessonsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        return LoadAsync(
            _db.GrammarLessonProgress
                .AsNoTracking()
                .Where(lesson => lesson.UpdatedAt >= from && lesson.UpdatedAt < to)
                .Select(lesson => new GrammarLessonFact(
                    lesson.LearnerId,
                    lesson.Topic,
                    lesson.UpdatedAt,
                    lesson.CompletedAt != null)),
            take,
            cancellationToken);
    }

    public Task<IReadOnlyList<VideoWatchFact>> GetVideoWatchesAsync(
        AnalyticsDateRange range,
        VideoWatchMoment moment,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;
        var watches = _db.VideoWatches.AsNoTracking();

        var facts = moment == VideoWatchMoment.LastProgress
            ? watches
                .Where(watch => watch.ProgressAt >= from && watch.ProgressAt < to)
                .Select(watch => new VideoWatchFact(
                    watch.LearnerId, watch.VideoId, watch.ProgressAt!.Value, watch.Duration, watch.WatchedPercent))
            : watches
                .Where(watch => watch.CreatedAt >= from && watch.CreatedAt < to)
                .Select(watch => new VideoWatchFact(
                    watch.LearnerId, watch.VideoId, watch.CreatedAt, watch.Duration, watch.WatchedPercent));

        return LoadAsync(facts, take, cancellationToken);
    }

    public Task<IReadOnlyList<NewsReadingFact>> GetNewsReadingsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        return LoadAsync(
            _db.NewsReadings
                .AsNoTracking()
                .Where(reading => reading.OpenedAt >= from && reading.OpenedAt < to)
                .Select(reading => new NewsReadingFact(
                    reading.LearnerId,
                    reading.ArticleId,
                    reading.OpenedAt,
                    reading.CompletedAt != null)),
            take,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetCourseCodesAsync(
        IReadOnlyCollection<Guid> courseIds,
        CancellationToken cancellationToken)
        => await _db.Courses
            .AsNoTracking()
            .Where(course => courseIds.Contains(course.Id))
            .Select(course => new { course.Id, course.Code })
            .Select(course => new { course.Id, course.Code })
            .ToDictionaryAsync(course => course.Id, course => course.Code, cancellationToken);

    public async Task<IReadOnlyDictionary<int, string>> GetDictationCourseCodesAsync(
        IReadOnlyCollection<int> courseIds,
        CancellationToken cancellationToken)
        => await _db.DictationCourses
            .AsNoTracking()
            .Where(course => courseIds.Contains(course.Id))
            .Select(course => new { course.Id, course.Code })
            .Select(course => new { course.Id, course.Code })
            .ToDictionaryAsync(course => course.Id, course => course.Code, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetStageSlugsAsync(
        IReadOnlyCollection<Guid> stageIds,
        CancellationToken cancellationToken)
        => await _db.VocabularyStages
            .AsNoTracking()
            .Where(stage => stageIds.Contains(stage.Id))
            .Select(stage => new { stage.Id, stage.Slug })
            .ToDictionaryAsync(stage => stage.Id, stage => stage.Slug, cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> GetVideoTitlesAsync(
        IReadOnlyCollection<string> videoIds,
        CancellationToken cancellationToken)
        => await _db.VideoCatalogItems
            .AsNoTracking()
            .Where(video => videoIds.Contains(video.VideoId))
            .Select(video => new { video.VideoId, video.Title })
            .ToDictionaryAsync(video => video.VideoId, video => video.Title, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetArticleSlugsAsync(
        IReadOnlyCollection<Guid> articleIds,
        CancellationToken cancellationToken)
        => await _db.NewsArticles
            .AsNoTracking()
            .Where(article => articleIds.Contains(article.Id))
            .Select(article => new { article.Id, article.Slug })
            .ToDictionaryAsync(article => article.Id, article => article.Slug, cancellationToken);

    private static async Task<IReadOnlyList<T>> LoadAsync<T>(
        IQueryable<T> query,
        int take,
        CancellationToken cancellationToken)
        => await query.Take(take).ToListAsync(cancellationToken);
}
