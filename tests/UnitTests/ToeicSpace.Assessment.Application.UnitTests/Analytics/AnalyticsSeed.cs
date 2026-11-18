using ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;
using ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Infrastructure.Services.Analytics;

namespace ToeicSpace.Assessment.Application.UnitTests.Analytics;

/// <summary>Attempts and answers for analytics tests. The clock of <see cref="TestDatabase"/> is 2026-09-14.</summary>
internal static class AnalyticsSeed
{
    public const string PseudonymKey = "unit-test-pseudonym-key-0123456789abcdef";

    public static readonly DateTime Recent = new(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc);

    public static ToeicAttempt AddAttempt(
        this TestDatabase database,
        Guid userId,
        Guid? testId,
        DateTime? start = null,
        AttemptStatus status = AttemptStatus.Completed,
        int score = 500,
        AttemptMode mode = AttemptMode.FullTest,
        params (ToeicQuestion Question, bool Correct)[] answers)
    {
        var attempt = new ToeicAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TestId = testId,
            StartTime = start ?? Recent,
            EndTime = (start ?? Recent).AddMinutes(90),
            DurationSeconds = 5400,
            TotalScore = score,
            ListeningScore = score / 2,
            ReadingScore = score - (score / 2),
            Status = status,
            Mode = mode
        };

        foreach (var (question, correct) in answers)
        {
            attempt.Answers.Add(new ToeicAttemptAnswer
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                UserAnswer = correct ? question.CorrectAnswer : AnswerOption.A,
                CorrectAnswer = question.CorrectAnswer,
                IsCorrect = correct,
                TimeSpentSeconds = 30
            });
        }

        database.Context.ToeicAttempts.Add(attempt);
        database.Context.SaveChanges();

        return attempt;
    }

    public static List<Guid> Learners(int count)
        => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    public static Task<AnalyticsResultDto> QueryAsync(this TestDatabase database, QueryAnalyticsQuery query)
        => new QueryAnalyticsHandler(database.Context, database.Time).Handle(query, CancellationToken.None);

    public static Task<AnalyticsEventsDto> EventsAsync(
        this TestDatabase database,
        GetAnalyticsEventsQuery query,
        ILearnerPseudonymizer? pseudonymizer = null)
        => new GetAnalyticsEventsHandler(database.Context, pseudonymizer ?? Pseudonymizer(), database.Time)
            .Handle(query, CancellationToken.None);

    public static ILearnerPseudonymizer Pseudonymizer(string? key = PseudonymKey)
        => new HmacLearnerPseudonymizer(new AnalyticsOptions { PseudonymKey = key });

    public static object? Cell(this AnalyticsResultDto result, int row, string column)
        => result.Rows[row][column];
}
