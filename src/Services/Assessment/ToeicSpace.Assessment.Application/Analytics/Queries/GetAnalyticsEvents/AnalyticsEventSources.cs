using ToeicSpace.Assessment.Application.Interfaces.Persistence;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;

public sealed record AttemptEventRow(
    Guid Id,
    Guid UserId,
    Guid? TestId,
    AttemptMode Mode,
    AttemptStatus Status,
    DateTime StartTime,
    DateTime? EndTime,
    int DurationSeconds,
    int TotalScore,
    int ListeningScore,
    int ReadingScore,
    int TotalCorrect,
    int TotalQuestions);

public sealed record AnswerEventRow(
    Guid Id,
    Guid AttemptId,
    Guid UserId,
    Guid? TestId,
    AttemptMode Mode,
    DateTime StartTime,
    Guid QuestionId,
    ToeicPart Part,
    QuestionDifficulty Difficulty,
    bool Answered,
    bool Correct,
    int TimeSpentSeconds);

/// <summary>
/// Keyset pages ordered by (attempt start time, row ID). One row more than the limit is read to know
/// whether another page exists.
/// </summary>
public static class AnalyticsEventSources
{
    public static IQueryable<AttemptEventRow> Attempts(
        IAssessmentDbContext context,
        AnalyticsEventsRequest request)
    {
        var from = request.Range.FromUtc;
        var to = request.Range.ToUtcExclusive;

        var query = context.ToeicAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StartTime >= from && attempt.StartTime < to);

        if (request.Cursor is { } cursor)
        {
            var time = cursor.StartTime;
            var id = cursor.Id;
            query = query.Where(attempt => attempt.StartTime > time
                || (attempt.StartTime == time && attempt.Id.CompareTo(id) > 0));
        }

        return query
            .OrderBy(attempt => attempt.StartTime)
            .ThenBy(attempt => attempt.Id)
            .Take(request.Limit + 1)
            .Select(attempt => new AttemptEventRow(
                attempt.Id,
                attempt.UserId,
                attempt.TestId,
                attempt.Mode,
                attempt.Status,
                attempt.StartTime,
                attempt.EndTime,
                attempt.DurationSeconds,
                attempt.TotalScore,
                attempt.ListeningScore,
                attempt.ReadingScore,
                attempt.TotalCorrect,
                attempt.TotalQuestions));
    }

    /// <summary>Soft-deleted questions stay included so history does not change when content is removed.</summary>
    public static IQueryable<AnswerEventRow> Answers(
        IAssessmentDbContext context,
        AnalyticsEventsRequest request)
    {
        var from = request.Range.FromUtc;
        var to = request.Range.ToUtcExclusive;

        var query = context.ToeicAttemptAnswers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(answer => answer.Attempt.StartTime >= from && answer.Attempt.StartTime < to);

        if (request.Cursor is { } cursor)
        {
            var time = cursor.StartTime;
            var id = cursor.Id;
            query = query.Where(answer => answer.Attempt.StartTime > time
                || (answer.Attempt.StartTime == time && answer.Id.CompareTo(id) > 0));
        }

        return query
            .OrderBy(answer => answer.Attempt.StartTime)
            .ThenBy(answer => answer.Id)
            .Take(request.Limit + 1)
            .Select(answer => new AnswerEventRow(
                answer.Id,
                answer.AttemptId,
                answer.Attempt.UserId,
                answer.Attempt.TestId,
                answer.Attempt.Mode,
                answer.Attempt.StartTime,
                answer.QuestionId,
                answer.Question.Part,
                answer.Question.DifficultyLevel,
                answer.UserAnswer != null,
                answer.IsCorrect,
                answer.TimeSpentSeconds));
    }
}
