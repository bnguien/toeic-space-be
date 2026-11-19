using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.CleanupAttempts;

public sealed class CleanupAttemptsHandler : IRequestHandler<CleanupAttemptsCommand, AttemptCleanupResult>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptContentReader _contentReader;
    private readonly AttemptGrader _grader;
    private readonly ILogger<CleanupAttemptsHandler> _logger;

    public CleanupAttemptsHandler(
        IAssessmentDbContext context,
        AttemptAccess access,
        AttemptContentReader contentReader,
        AttemptGrader grader,
        ILogger<CleanupAttemptsHandler> logger)
    {
        _context = context;
        _access = access;
        _contentReader = contentReader;
        _grader = grader;
        _logger = logger;
    }

    public async Task<AttemptCleanupResult> Handle(CleanupAttemptsCommand request, CancellationToken cancellationToken)
    {
        var submitted = await AutoSubmitAsync(cancellationToken);
        var deleted = await DeleteExpiredAsync(cancellationToken);

        return new AttemptCleanupResult(submitted, deleted);
    }

    private async Task<int> AutoSubmitAsync(CancellationToken cancellationToken)
    {
        var cutoff = _access.Now - AttemptAccess.AutoSubmitDelay;
        var candidates = await _access.Eligible()
            .AsNoTracking()
            .Where(attempt => attempt.Status == AttemptStatus.InProgress
                && attempt.Mode == AttemptMode.FullTest
                && attempt.Deadline != null
                && attempt.Deadline <= cutoff)
            .OrderBy(attempt => attempt.Deadline)
            .Take(AttemptCleanupResult.BatchSize)
            .Select(attempt => new { attempt.Id, attempt.UserId, attempt.TestId, attempt.PracticeSetId })
            .ToListAsync(cancellationToken);

        var submitted = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                submitted += await _context.ExecuteAttemptUpdateAsync(candidate.UserId,
                    candidate.TestId ?? candidate.PracticeSetId!.Value, async () =>
                    {
                        var attempt = await _access.Eligible()
                            .SingleOrDefaultAsync(item => item.Id == candidate.Id, cancellationToken);

                        if (attempt is null || attempt.Status != AttemptStatus.InProgress)
                        {
                            return 0;
                        }

                        var content = await _contentReader.LoadAsync(attempt, trackAnswers: true, cancellationToken);
                        await _grader.SubmitAsync(content, _access.Now, cancellationToken);
                        return 1;
                    }, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                _logger.LogError("Auto-submit failed for attempt {AttemptId}: {ErrorType}", candidate.Id, exception.GetType().Name);
            }
        }

        if (candidates.Count > 0)
        {
            _logger.LogInformation("Auto-submit checked {Checked}, submitted {Submitted}, failed {Failed}",
                candidates.Count, submitted, failed);
        }

        return submitted;
    }

    private async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = _access.Now.AddDays(-AttemptAccess.RetentionDays);
        var candidates = await _access.Eligible()
            .AsNoTracking()
            .Where(attempt => attempt.Status == AttemptStatus.InProgress
                && attempt.Mode != AttemptMode.FullTest
                && attempt.LastActivityAt <= cutoff)
            .OrderBy(attempt => attempt.LastActivityAt)
            .Take(AttemptCleanupResult.BatchSize)
            .Select(attempt => new { attempt.Id, attempt.UserId, attempt.TestId, attempt.PracticeSetId })
            .ToListAsync(cancellationToken);

        var deleted = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            var sourceId = candidate.TestId ?? candidate.PracticeSetId!.Value;

            try
            {
                deleted += await _context.ExecuteAttemptUpdateAsync(candidate.UserId, sourceId, async () =>
                {
                    var attempt = await _access.Eligible()
                        .SingleOrDefaultAsync(item => item.Id == candidate.Id, cancellationToken);

                    if (attempt is null || !_access.IsExpired(attempt))
                    {
                        return 0;
                    }

                    await _access.DeleteAsync(attempt, await _access.FenceAsync(candidate.UserId, sourceId, cancellationToken),
                        cancellationToken);
                    return 1;
                }, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                _logger.LogError("Practice cleanup failed for {AttemptId}: {ErrorType}", candidate.Id, exception.GetType().Name);
            }
        }

        _logger.LogInformation(
            "Practice cleanup checked {Checked}, deleted {Deleted}, skipped {Skipped}, failed {Failed} at {Utc}",
            candidates.Count, deleted, candidates.Count - deleted - failed, failed, _access.Now);

        return deleted;
    }
}
