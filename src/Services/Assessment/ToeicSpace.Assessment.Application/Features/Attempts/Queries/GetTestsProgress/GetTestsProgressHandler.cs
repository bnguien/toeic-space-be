using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetTestsProgress;

public sealed class GetTestsProgressHandler : IRequestHandler<GetTestsProgressQuery, IReadOnlyList<TestProgressDto>>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;

    public GetTestsProgressHandler(IAssessmentDbContext context, AttemptAccess access)
    {
        _context = context;
        _access = access;
    }

    public async Task<IReadOnlyList<TestProgressDto>> Handle(GetTestsProgressQuery request, CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var attempts = await _access.Eligible()
            .AsNoTracking()
            .Where(attempt => attempt.UserId == owner && attempt.TestId != null)
            .ToListAsync(cancellationToken);

        if (attempts.Count == 0)
        {
            return [];
        }

        var active = attempts
            .Where(attempt => attempt.Status == AttemptStatus.InProgress && !_access.IsExpired(attempt))
            .GroupBy(attempt => attempt.TestId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(attempt => attempt.StartTime).First());
        var activeIds = active.Values.Select(attempt => attempt.Id).ToArray();
        var answered = await _context.ToeicAttemptAnswers
            .AsNoTracking()
            .Where(answer => activeIds.Contains(answer.AttemptId) && answer.UserAnswer != null)
            .GroupBy(answer => answer.AttemptId)
            .Select(group => new { AttemptId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.AttemptId, row => row.Count, cancellationToken);

        var testIds = attempts.Select(attempt => attempt.TestId!.Value).Distinct().ToArray();
        var testTotals = await _context.ToeicTests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(test => testIds.Contains(test.Id))
            .ToDictionaryAsync(test => test.Id, test => test.TotalQuestions, cancellationToken);

        return attempts
            .GroupBy(attempt => attempt.TestId!.Value)
            .Select(group =>
            {
                var current = active.GetValueOrDefault(group.Key);
                var completed = group
                    .Where(attempt => attempt.Status == AttemptStatus.Completed)
                    .OrderByDescending(attempt => attempt.EndTime)
                    .ToList();
                var best = completed
                    .Select(attempt => AttemptScoreCalculator.StoredScaled(attempt).Total)
                    .Where(total => total.HasValue)
                    .Max();
                var status = current is not null
                    ? TestProgressStatus.InProgress
                    : completed.Count > 0 ? TestProgressStatus.Completed : TestProgressStatus.NotStarted;

                return new TestProgressDto(
                    group.Key,
                    status,
                    current?.Id,
                    current?.Mode,
                    current is null ? 0 : answered.GetValueOrDefault(current.Id),
                    current?.TotalQuestions ?? testTotals.GetValueOrDefault(group.Key),
                    completed.Count,
                    best,
                    completed.FirstOrDefault()?.Id);
            })
            .ToList();
    }
}
