using ToeicSpace.Assessment.Application.Common.Querying;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeStats;

public sealed class GetPracticeStatsHandler : IRequestHandler<GetPracticeStatsQuery, PracticeStatsDto>
{
    public const int TrendLength = 20;

    public const int RecentLength = 8;

    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptSummaryReader _summaries;

    public GetPracticeStatsHandler(IAssessmentDbContext context, AttemptAccess access, AttemptSummaryReader summaries)
    {
        _context = context;
        _access = access;
        _summaries = summaries;
    }

    public async Task<PracticeStatsDto> Handle(GetPracticeStatsQuery request, CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var mine = _access.Eligible().AsNoTracking().Where(attempt => attempt.UserId == owner);
        var fullTests = mine.Where(attempt => attempt.Status == AttemptStatus.Completed
            && attempt.Mode == AttemptMode.FullTest
            && attempt.TestId != null);

        var testsCompleted = await fullTests.Select(attempt => attempt.TestId).Distinct().CountAsync(cancellationToken);
        var testsTotal = await _context.ToeicTests.AsNoTracking().VisibleTo(canManageContent: false).CountAsync(cancellationToken);

        var trendAttempts = await fullTests
            .OrderByDescending(attempt => attempt.EndTime)
            .Take(TrendLength)
            .ToListAsync(cancellationToken);
        var trend = trendAttempts
            .OrderBy(attempt => attempt.EndTime)
            .Select(attempt =>
            {
                var (listening, reading, total) = AttemptScoreCalculator.StoredScaled(attempt);
                return new ScoreTrendPointDto(attempt.Id, AttemptAccess.Utc(attempt.EndTime ?? attempt.StartTime),
                    total, listening, reading);
            })
            .ToList();

        var partRows = await _context.ToeicAttemptAnswers
            .AsNoTracking()
            .Where(answer => answer.Attempt.UserId == owner
                && answer.SnapshotQuestionId != null
                && answer.UserAnswer != null
                && (answer.Attempt.Status == AttemptStatus.Completed || answer.CheckedAt != null))
            .Join(_context.ToeicQuestions.IgnoreQueryFilters(),
                answer => answer.QuestionId,
                question => question.Id,
                (answer, question) => new { question.Part, answer.IsCorrect })
            .GroupBy(row => row.Part)
            .Select(group => new { Part = group.Key, Answered = group.Count(), Correct = group.Count(row => row.IsCorrect) })
            .ToListAsync(cancellationToken);
        var byPart = AttemptParts.All
            .Select(part =>
            {
                var row = partRows.FirstOrDefault(item => item.Part == part);
                return new PartStatsDto((int)part, row?.Answered ?? 0, row?.Correct ?? 0);
            })
            .ToList();
        var answered = byPart.Sum(part => part.Answered);
        var correct = byPart.Sum(part => part.Correct);

        var recentAttempts = await mine
            .Where(attempt => attempt.Status != AttemptStatus.Abandoned)
            .OrderByDescending(attempt => attempt.StartTime)
            .ThenByDescending(attempt => attempt.Id)
            .Take(RecentLength)
            .ToListAsync(cancellationToken);

        return new PracticeStatsDto(
            testsCompleted,
            testsTotal,
            answered,
            answered == 0 ? 0 : Math.Round((double)correct / answered, 4),
            byPart,
            trend,
            await _summaries.SummarizeAsync(recentAttempts, cancellationToken));
    }
}
