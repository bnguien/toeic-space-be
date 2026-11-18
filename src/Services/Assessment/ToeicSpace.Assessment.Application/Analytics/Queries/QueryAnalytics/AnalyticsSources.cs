using ToeicSpace.Assessment.Application.Interfaces.Persistence;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

public sealed record AttemptFact(
    Guid UserId,
    Guid? TestId,
    DateTime StartTime,
    AttemptStatus Status,
    AttemptMode Mode,
    int DurationSeconds,
    int TotalScore,
    int ListeningScore,
    int ReadingScore);

/// <summary>Answers of one learner on one day, pre-aggregated in the database by test, mode, part and difficulty.</summary>
public sealed record AnswerPartial(
    Guid UserId,
    DateTime Day,
    Guid? TestId,
    AttemptMode Mode,
    ToeicPart Part,
    QuestionDifficulty Difficulty,
    int Answers,
    int Correct,
    int Blank,
    int Timed,
    int TimeSpentSeconds);

public sealed record QuestionPartial(
    Guid QuestionId,
    ToeicPart Part,
    QuestionDifficulty Difficulty,
    int Answers,
    int Correct,
    int Learners);

public sealed record PracticeSetPartial(
    Guid PracticeSetId,
    int Answers,
    int Correct,
    int Learners);

public sealed record LearnerFirstAttempt(
    Guid UserId,
    DateTime FirstStart);

public sealed record LearnerActiveDay(
    Guid UserId,
    DateTime Day);

/// <summary>
/// The fixed EF Core queries behind the analytics metrics. Request values only become typed parameters
/// (dates, IDs, enum values); grouping keys and columns never come from input.
/// </summary>
public static class AnalyticsSources
{
    public static IQueryable<AttemptFact> Attempts(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        bool completedOnly = false)
    {
        var from = request.Range!.FromUtc;
        var to = request.Range.ToUtcExclusive;

        var query = context.ToeicAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StartTime >= from && attempt.StartTime < to);

        if (request.TestId is { } testId)
        {
            query = query.Where(attempt => attempt.TestId == testId);
        }

        if (request.Mode is { } mode)
        {
            query = query.Where(attempt => attempt.Mode == mode);
        }

        if (completedOnly)
        {
            query = query.Where(attempt => attempt.Status == AttemptStatus.Completed);
        }
        else if (request.AttemptStatus is { } status)
        {
            query = query.Where(attempt => attempt.Status == status);
        }

        return query.Select(attempt => new AttemptFact(
            attempt.UserId,
            attempt.TestId,
            attempt.StartTime,
            attempt.Status,
            attempt.Mode,
            attempt.DurationSeconds,
            attempt.TotalScore,
            attempt.ListeningScore,
            attempt.ReadingScore));
    }

    public static IQueryable<AnswerPartial> AnswerPartials(
        IAssessmentDbContext context,
        AnalyticsRequest request)
        => Answers(context, request)
            .Select(answer => new
            {
                answer.Attempt.UserId,
                Day = answer.Attempt.StartTime.Date,
                answer.Attempt.TestId,
                answer.Attempt.Mode,
                answer.Question.Part,
                answer.Question.DifficultyLevel,
                answer.IsCorrect,
                Blank = answer.UserAnswer == null,
                answer.TimeSpentSeconds
            })
            .GroupBy(answer => new
            {
                answer.UserId,
                answer.Day,
                answer.TestId,
                answer.Mode,
                answer.Part,
                answer.DifficultyLevel
            })
            .Select(group => new AnswerPartial(
                group.Key.UserId,
                group.Key.Day,
                group.Key.TestId,
                group.Key.Mode,
                group.Key.Part,
                group.Key.DifficultyLevel,
                group.Count(),
                group.Count(answer => answer.IsCorrect),
                group.Count(answer => answer.Blank),
                group.Count(answer => answer.TimeSpentSeconds > 0),
                group.Sum(answer => answer.TimeSpentSeconds)));

    public static IQueryable<QuestionPartial> QuestionPartials(
        IAssessmentDbContext context,
        AnalyticsRequest request)
        => Answers(context, request)
            .Select(answer => new
            {
                answer.QuestionId,
                answer.Question.Part,
                answer.Question.DifficultyLevel,
                answer.Attempt.UserId,
                answer.IsCorrect
            })
            // Two-level grouping (learner, then question) counts distinct learners in one pass.
            .GroupBy(answer => new { answer.QuestionId, answer.Part, answer.DifficultyLevel, answer.UserId })
            .Select(group => new
            {
                group.Key.QuestionId,
                group.Key.Part,
                group.Key.DifficultyLevel,
                Answers = group.Count(),
                Correct = group.Count(answer => answer.IsCorrect)
            })
            .GroupBy(learner => new { learner.QuestionId, learner.Part, learner.DifficultyLevel })
            .Select(group => new QuestionPartial(
                group.Key.QuestionId,
                group.Key.Part,
                group.Key.DifficultyLevel,
                group.Sum(learner => learner.Answers),
                group.Sum(learner => learner.Correct),
                group.Count()));

    /// <summary>An answer counts for every live practice set that contains its question.</summary>
    public static IQueryable<PracticeSetPartial> PracticeSetPartials(
        IAssessmentDbContext context,
        AnalyticsRequest request)
        => Answers(context, request)
            .Select(answer => new { answer.QuestionId, answer.Attempt.UserId, answer.IsCorrect })
            .Join(context.ToeicPracticeSetItems, answer => answer.QuestionId, item => item.QuestionId,
                (answer, item) => new { item.PracticeSetId, answer.UserId, answer.IsCorrect })
            .Join(context.ToeicPracticeSets.Where(practiceSet => practiceSet.DeletedAt == null),
                row => row.PracticeSetId, practiceSet => practiceSet.Id,
                (row, _) => row)
            .GroupBy(row => new { row.PracticeSetId, row.UserId })
            .Select(group => new
            {
                group.Key.PracticeSetId,
                Answers = group.Count(),
                Correct = group.Count(row => row.IsCorrect)
            })
            .GroupBy(learner => learner.PracticeSetId)
            .Select(group => new PracticeSetPartial(
                group.Key,
                group.Sum(learner => learner.Answers),
                group.Sum(learner => learner.Correct),
                group.Count()));

    public static IQueryable<Guid> AnsweredQuestionIds(
        IAssessmentDbContext context,
        AnalyticsRequest request)
        => Answers(context, request)
            .Select(answer => answer.QuestionId)
            .Distinct();

    /// <summary>Learners whose first attempt ever starts inside the range.</summary>
    public static IQueryable<LearnerFirstAttempt> FirstAttempts(
        IAssessmentDbContext context,
        AnalyticsRequest request)
    {
        var from = request.Range!.FromUtc;
        var to = request.Range.ToUtcExclusive;

        return context.ToeicAttempts
            .AsNoTracking()
            .GroupBy(attempt => attempt.UserId)
            .Select(group => new { UserId = group.Key, FirstStart = group.Min(attempt => attempt.StartTime) })
            .Where(learner => learner.FirstStart >= from && learner.FirstStart < to)
            .Select(learner => new LearnerFirstAttempt(learner.UserId, learner.FirstStart));
    }

    public static IQueryable<LearnerActiveDay> ActiveDays(
        IAssessmentDbContext context,
        DateTime fromUtc,
        DateTime toUtcExclusive)
        => context.ToeicAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StartTime >= fromUtc && attempt.StartTime < toUtcExclusive)
            .Select(attempt => new { attempt.UserId, Day = attempt.StartTime.Date })
            .Distinct()
            .Select(row => new LearnerActiveDay(row.UserId, row.Day));

    /// <summary>
    /// Graded answers of attempts started in the range. Soft-deleted questions stay included so history does
    /// not change when content is removed.
    /// </summary>
    private static IQueryable<ToeicAttemptAnswer> Answers(
        IAssessmentDbContext context,
        AnalyticsRequest request)
    {
        var from = request.Range!.FromUtc;
        var to = request.Range.ToUtcExclusive;

        var query = context.ToeicAttemptAnswers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(answer => answer.Attempt.StartTime >= from && answer.Attempt.StartTime < to);

        if (request.TestId is { } testId)
        {
            query = query.Where(answer => answer.Attempt.TestId == testId);
        }

        if (request.Mode is { } mode)
        {
            query = query.Where(answer => answer.Attempt.Mode == mode);
        }

        if (request.Part is { } part)
        {
            query = query.Where(answer => answer.Question.Part == part);
        }

        if (request.Section is { } section)
        {
            query = query.Where(answer => answer.Question.Section == section);
        }

        if (request.Difficulty is { } difficulty)
        {
            query = query.Where(answer => answer.Question.DifficultyLevel == difficulty);
        }

        return query;
    }
}
