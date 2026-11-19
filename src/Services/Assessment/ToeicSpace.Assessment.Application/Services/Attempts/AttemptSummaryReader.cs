using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Builds history rows from stored attempt columns without reading snapshots. Source titles come from the
/// live test/practice set (including soft-deleted ones), falling back to the attempt title.
/// </summary>
public sealed class AttemptSummaryReader
{
    private readonly IAssessmentDbContext _context;

    public AttemptSummaryReader(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AttemptSummaryDto>> SummarizeAsync(
        IReadOnlyList<ToeicAttempt> attempts,
        CancellationToken cancellationToken)
    {
        if (attempts.Count == 0)
        {
            return [];
        }

        var attemptIds = attempts.Select(attempt => attempt.Id).ToArray();
        var answered = await _context.ToeicAttemptAnswers
            .AsNoTracking()
            .Where(answer => attemptIds.Contains(answer.AttemptId) && answer.UserAnswer != null)
            .GroupBy(answer => answer.AttemptId)
            .Select(group => new { AttemptId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.AttemptId, row => row.Count, cancellationToken);

        var testIds = attempts.Where(attempt => attempt.TestId.HasValue).Select(attempt => attempt.TestId!.Value).Distinct().ToArray();
        var tests = await _context.ToeicTests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(test => testIds.Contains(test.Id))
            .Select(test => new AttemptSourceDto(AttemptSourceKind.Test, test.Id, test.Code, test.Title, test.Category,
                null, null, null))
            .ToDictionaryAsync(source => source.Id, cancellationToken);

        var setIds = attempts.Where(attempt => attempt.PracticeSetId.HasValue).Select(attempt => attempt.PracticeSetId!.Value).Distinct().ToArray();
        var sets = await _context.ToeicPracticeSets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(set => setIds.Contains(set.Id))
            .Select(set => new { set.Id, set.Code, set.Title, set.Part, set.Level, set.TargetScore })
            .ToListAsync(cancellationToken);
        var setSources = sets.ToDictionary(
            set => set.Id,
            set => new AttemptSourceDto(AttemptSourceKind.PracticeSet, set.Id, set.Code, set.Title, null,
                (int)set.Part, set.Level, set.TargetScore));

        return attempts
            .Select(attempt =>
            {
                var source = attempt.TestId is { } testId
                    ? tests.GetValueOrDefault(testId) ?? Fallback(attempt, AttemptSourceKind.Test, testId)
                    : setSources.GetValueOrDefault(attempt.PracticeSetId!.Value)
                        ?? Fallback(attempt, AttemptSourceKind.PracticeSet, attempt.PracticeSetId!.Value);
                var (listening, reading, total) = AttemptScoreCalculator.StoredScaled(attempt);

                return new AttemptSummaryDto(
                    attempt.Id,
                    attempt.Mode,
                    attempt.Status,
                    attempt.Title,
                    source,
                    AttemptParts.Parse(attempt.SelectedParts).Select(part => (int)part).ToList(),
                    AttemptAccess.Utc(attempt.StartTime),
                    attempt.EndTime is { } endTime ? AttemptAccess.Utc(endTime) : null,
                    attempt.DurationSeconds,
                    attempt.TotalQuestions,
                    answered.GetValueOrDefault(attempt.Id),
                    attempt.TotalCorrect,
                    listening,
                    reading,
                    total);
            })
            .ToList();
    }

    private static AttemptSourceDto Fallback(ToeicAttempt attempt, AttemptSourceKind kind, Guid id)
        => new(kind, id, string.Empty, attempt.Title, null, null, null, null);
}
