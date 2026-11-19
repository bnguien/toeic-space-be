using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// A learner's answers across attempts, keyed by source (bank) question. An answer is graded once its attempt
/// is submitted or the learner checked it during practice.
/// </summary>
public sealed class AnswerHistoryReader
{
    public const int HistoryLimit = 5;

    private readonly IAssessmentDbContext _context;

    public AnswerHistoryReader(IAssessmentDbContext context)
    {
        _context = context;
    }

    /// <summary>Graded answers to the given source questions in the learner's other attempts, newest first.</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<AnswerHistoryDto>>> HistoryAsync(
        Guid owner,
        Guid excludedAttemptId,
        IReadOnlyCollection<Guid> sourceQuestionIds,
        CancellationToken cancellationToken)
    {
        if (sourceQuestionIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<AnswerHistoryDto>>();
        }

        var rows = await GradedAnswers(owner)
            .Where(answer => answer.AttemptId != excludedAttemptId && sourceQuestionIds.Contains(answer.QuestionId))
            .Select(answer => new
            {
                answer.AttemptId,
                answer.QuestionId,
                answer.UserAnswer,
                answer.IsCorrect,
                answer.CheckedAt,
                answer.Attempt.EndTime,
                answer.UpdatedAt,
                answer.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.QuestionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AnswerHistoryDto>)group
                    .Select(row => new AnswerHistoryDto(
                        row.AttemptId,
                        AttemptAccess.Utc(row.CheckedAt ?? row.EndTime ?? row.UpdatedAt ?? row.CreatedAt),
                        row.UserAnswer,
                        row.UserAnswer is null ? null : row.IsCorrect))
                    .OrderByDescending(item => item.AnsweredAt)
                    .Take(HistoryLimit)
                    .ToList());
    }

    /// <summary>The latest graded, non-empty answer per source question.</summary>
    public async Task<IReadOnlyDictionary<Guid, GradedAnswer>> LatestGradedAsync(
        Guid owner,
        IReadOnlyCollection<Guid>? sourceQuestionIds,
        CancellationToken cancellationToken)
    {
        var query = GradedAnswers(owner).Where(answer => answer.UserAnswer != null);

        if (sourceQuestionIds is not null)
        {
            query = query.Where(answer => sourceQuestionIds.Contains(answer.QuestionId));
        }

        var rows = await query
            .Select(answer => new
            {
                answer.QuestionId,
                answer.IsCorrect,
                answer.CheckedAt,
                answer.Attempt.EndTime,
                answer.UpdatedAt,
                answer.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new GradedAnswer(
                row.QuestionId,
                row.IsCorrect,
                AttemptAccess.Utc(row.CheckedAt ?? row.EndTime ?? row.UpdatedAt ?? row.CreatedAt)))
            .GroupBy(answer => answer.SourceQuestionId)
            .ToDictionary(group => group.Key, group => group.MaxBy(answer => answer.GradedAt)!);
    }

    /// <summary>Source questions the learner has ever chosen an answer for, graded or not.</summary>
    public async Task<IReadOnlySet<Guid>> AnsweredQuestionIdsAsync(
        Guid owner,
        IReadOnlyCollection<Guid> sourceQuestionIds,
        CancellationToken cancellationToken)
    {
        var ids = await _context.ToeicAttemptAnswers
            .AsNoTracking()
            .Where(answer => answer.Attempt.UserId == owner
                && answer.UserAnswer != null
                && sourceQuestionIds.Contains(answer.QuestionId))
            .Select(answer => answer.QuestionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private IQueryable<ToeicAttemptAnswer> GradedAnswers(Guid owner)
        => _context.ToeicAttemptAnswers
            .AsNoTracking()
            .Where(answer => answer.Attempt.UserId == owner
                && answer.SnapshotQuestionId != null
                && (answer.Attempt.Status == AttemptStatus.Completed || answer.CheckedAt != null));
}
