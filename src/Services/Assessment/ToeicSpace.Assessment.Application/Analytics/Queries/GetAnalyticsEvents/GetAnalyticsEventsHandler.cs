using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Interfaces.Security;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;

/// <summary>
/// Event rows for free-form analysis. Learner and attempt IDs are replaced by keyed pseudonyms; answer keys
/// and chosen options are left out because together with <c>correct</c> they would reveal the key.
/// </summary>
public sealed class GetAnalyticsEventsHandler : IRequestHandler<GetAnalyticsEventsQuery, AnalyticsEventsDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ILearnerPseudonymizer _pseudonymizer;
    private readonly TimeProvider _timeProvider;

    public GetAnalyticsEventsHandler(
        IAssessmentDbContext context,
        ILearnerPseudonymizer pseudonymizer,
        TimeProvider timeProvider)
    {
        _context = context;
        _pseudonymizer = pseudonymizer;
        _timeProvider = timeProvider;
    }

    public async Task<AnalyticsEventsDto> Handle(
        GetAnalyticsEventsQuery query,
        CancellationToken cancellationToken)
    {
        if (!_pseudonymizer.IsConfigured)
        {
            throw new AppException(
                ErrorType.ServiceUnavailable,
                "analytics pseudonym key not configured",
                ErrorCodes.AnalyticsUnavailable);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var errors = new List<KeyValuePair<string, string>>();

        var request = AnalyticsEventsRequestParser.Parse(query, DateOnly.FromDateTime(now), errors)
            ?? throw new AppException(AnalyticsValues.ToErrorDictionary(errors));

        var (rows, nextCursor) = await AnalyticsExecution.WithTimeoutAsync(
            token => request.Type == AnalyticsEventTypes.Answer
                ? ReadAnswersAsync(request, token)
                : ReadAttemptsAsync(request, token),
            cancellationToken);

        return new AnalyticsEventsDto(
            request.Type,
            request.Range.From,
            request.Range.To,
            rows,
            nextCursor,
            DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    private async Task<(IReadOnlyList<AnalyticsEventDto> Rows, string? NextCursor)> ReadAttemptsAsync(
        AnalyticsEventsRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await AnalyticsEventSources.Attempts(_context, request).ToListAsync(cancellationToken);
        var page = rows.Take(request.Limit).ToList();
        var nextCursor = rows.Count > request.Limit
            ? new AnalyticsCursor(AnalyticsEventTypes.Attempt, page[^1].StartTime, page[^1].Id).Encode()
            : null;

        var events = page
            .Select(attempt => new AnalyticsEventDto(
                _pseudonymizer.Learner(attempt.UserId),
                _pseudonymizer.Attempt(attempt.Id),
                attempt.TestId,
                attempt.Mode,
                Utc(attempt.StartTime),
                Status: attempt.Status,
                EndedAt: attempt.EndTime is { } endTime ? Utc(endTime) : null,
                DurationSeconds: attempt.DurationSeconds,
                TotalScore: attempt.TotalScore,
                ListeningScore: attempt.ListeningScore,
                ReadingScore: attempt.ReadingScore,
                TotalCorrect: attempt.TotalCorrect,
                TotalQuestions: attempt.TotalQuestions))
            .ToList();

        return (events, nextCursor);
    }

    private async Task<(IReadOnlyList<AnalyticsEventDto> Rows, string? NextCursor)> ReadAnswersAsync(
        AnalyticsEventsRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await AnalyticsEventSources.Answers(_context, request).ToListAsync(cancellationToken);
        var page = rows.Take(request.Limit).ToList();
        var nextCursor = rows.Count > request.Limit
            ? new AnalyticsCursor(AnalyticsEventTypes.Answer, page[^1].StartTime, page[^1].Id).Encode()
            : null;

        var events = page
            .Select(answer => new AnalyticsEventDto(
                _pseudonymizer.Learner(answer.UserId),
                _pseudonymizer.Attempt(answer.AttemptId),
                answer.TestId,
                answer.Mode,
                Utc(answer.StartTime),
                QuestionId: answer.QuestionId,
                Part: answer.Part,
                Section: AnalyticsGrouping.SectionOf(answer.Part),
                Difficulty: answer.Difficulty,
                Answered: answer.Answered,
                Correct: answer.Correct,
                TimeSpentSeconds: answer.TimeSpentSeconds))
            .ToList();

        return (events, nextCursor);
    }

    private static DateTime Utc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
