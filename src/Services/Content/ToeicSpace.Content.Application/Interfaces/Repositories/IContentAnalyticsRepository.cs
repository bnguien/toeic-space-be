using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>
/// Fixed read-only queries behind the analytics metrics. Request values only become typed parameters (dates,
/// IDs, enum values); grouping keys and columns never come from input. Every list holds at most <c>take</c> rows
/// so the caller can tell when a selection is too large (see <see cref="AnalyticsExecution.LoadAsync{T}"/>).
/// Ranges are inclusive UTC days: <c>[FromUtc, ToUtcExclusive)</c>.
/// </summary>
public interface IContentAnalyticsRepository
{
    Task<IReadOnlyList<CourseFact>> GetCoursesAsync(
        CourseStatus? status,
        CourseLevel? level,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LessonPartial>> GetLessonCountsAsync(
        Guid? courseId,
        LessonStatus? status,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Vocabulary study sessions started in the range.</summary>
    Task<IReadOnlyList<StudySessionFact>> GetStudySessionsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Distinct (learner, UTC day) pairs with any recorded learning activity in the range.</summary>
    Task<IReadOnlyList<LearnerDay>> GetActiveLearnerDaysAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Word reviews in the range per learner and day, and per word when <paramref name="byWord"/> is set.</summary>
    Task<IReadOnlyList<ReviewPartial>> GetReviewPartialsAsync(
        AnalyticsDateRange range,
        bool byWord,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Reviews in the range that moved a word from below to at least <paramref name="masteredIntervalDays"/>.</summary>
    Task<IReadOnlyList<MasteredWordFact>> GetMasteredWordsAsync(
        AnalyticsDateRange range,
        int masteredIntervalDays,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Every (word, stage) membership of the vocabulary roadmap.</summary>
    Task<IReadOnlyList<WordStage>> GetWordStagesAsync(
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedWordPartial>> GetSavedWordPartialsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Dictation progress rows whose latest attempt falls in the range.</summary>
    Task<IReadOnlyList<DictationPartial>> GetDictationPartialsAsync(
        AnalyticsDateRange range,
        int? courseId,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GrammarAttemptFact>> GetGrammarAttemptsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Answered grammar question results whose latest answer falls in the range.</summary>
    Task<IReadOnlyList<GrammarAnswerPartial>> GetGrammarAnswerPartialsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Grammar topic progress rows last updated in the range.</summary>
    Task<IReadOnlyList<GrammarLessonFact>> GetGrammarLessonsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VideoWatchFact>> GetVideoWatchesAsync(
        AnalyticsDateRange range,
        VideoWatchMoment moment,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsReadingFact>> GetNewsReadingsAsync(
        AnalyticsDateRange range,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Course codes keyed by course ID, for the <c>labels</c> of a result.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetCourseCodesAsync(
        IReadOnlyCollection<Guid> courseIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, string>> GetDictationCourseCodesAsync(
        IReadOnlyCollection<int> courseIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetStageSlugsAsync(
        IReadOnlyCollection<Guid> stageIds,
        CancellationToken cancellationToken);

    /// <summary>Titles of catalogue videos keyed by YouTube video ID (videos outside the catalogue have none).</summary>
    Task<IReadOnlyDictionary<string, string>> GetVideoTitlesAsync(
        IReadOnlyCollection<string> videoIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetArticleSlugsAsync(
        IReadOnlyCollection<Guid> articleIds,
        CancellationToken cancellationToken);
}
