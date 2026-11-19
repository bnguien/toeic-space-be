using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Security;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Ownership, lifecycle and locking rules shared by every attempt use case.
/// All mutations of one learner's attempts on one source (test or practice set) are serialized by the
/// <see cref="PracticeAttemptState"/> fence row, locked with <c>FOR UPDATE</c> before anything else.
/// </summary>
public sealed class AttemptAccess
{
    /// <summary>In-progress practice is discarded after this many days without activity.</summary>
    public const int RetentionDays = 7;

    /// <summary>FullTest answers are still accepted this long after the deadline (network latency).</summary>
    public static readonly TimeSpan AnswerGrace = TimeSpan.FromSeconds(30);

    /// <summary>The background worker submits FullTest attempts this long after their deadline.</summary>
    public static readonly TimeSpan AutoSubmitDelay = TimeSpan.FromSeconds(60);

    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AttemptAccess(
        IAssessmentDbContext context,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// UTC now at MySQL <c>datetime(6)</c> precision, so a response built right after a write equals one read back later.
    /// </summary>
    public DateTime Now
    {
        get
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        }
    }

    public Guid RequireLearner()
        => _currentUser.IsAuthenticated && _currentUser.UserId is { } id && !_currentUser.CanManageContent
            ? id
            : throw AppException.Forbidden("Only authenticated students can access practice attempts.");

    public static AppException Unavailable()
        => AppException.NotFound("Practice attempt is unavailable.", ErrorCodes.AttemptUnavailable);

    public static AppException NotActive()
        => AppException.Conflict("This attempt is no longer active.", ErrorCodes.AttemptNotActive);

    public static AppException NotCompleted()
        => AppException.Conflict("This attempt has not been submitted yet.", ErrorCodes.AttemptNotCompleted);

    public static AppException TimeUp()
        => AppException.Conflict("The time limit of this test has passed.", ErrorCodes.AttemptTimeUp);

    public static AppException NothingToPractice()
        => AppException.Conflict("There are no questions to practice for this selection.", ErrorCodes.NothingToPractice);

    public static Guid SourceId(ToeicAttempt attempt)
        => attempt.TestId ?? attempt.PracticeSetId
            ?? throw new InvalidOperationException("An attempt always has a test or a practice set.");

    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>Untimed practice expires after <see cref="RetentionDays"/> without activity; timed tests are submitted instead.</summary>
    public bool IsExpired(ToeicAttempt attempt)
        => attempt.Status == AttemptStatus.InProgress
            && attempt.Mode != AttemptMode.FullTest
            && attempt.LastActivityAt <= Now.AddDays(-RetentionDays);

    public bool IsTimeUp(ToeicAttempt attempt)
        => attempt.Mode == AttemptMode.FullTest
            && attempt.Deadline is { } deadline
            && Now > deadline + AnswerGrace;

    /// <summary>Attempts with an immutable snapshot (legacy rows without one are not served).</summary>
    public IQueryable<ToeicAttempt> Eligible()
        => _context.ToeicAttempts.Where(attempt => attempt.LastActivityAt != null
            && attempt.Snapshot != null
            && attempt.Snapshot.SchemaVersion == 1);

    public IQueryable<ToeicAttempt> InProgressFor(Guid owner, Guid sourceId)
        => Eligible().Where(attempt => attempt.UserId == owner
            && attempt.Status == AttemptStatus.InProgress
            && (attempt.TestId == sourceId || attempt.PracticeSetId == sourceId));

    /// <summary>Loads an attempt of the current learner. Other users' attempts are reported as unavailable.</summary>
    public async Task<ToeicAttempt> LoadOwnedAsync(Guid id, bool track, CancellationToken cancellationToken)
    {
        var owner = RequireLearner();
        var query = track ? _context.ToeicAttempts : _context.ToeicAttempts.AsNoTracking();
        var attempt = await query.SingleOrDefaultAsync(item => item.Id == id && item.UserId == owner, cancellationToken)
            ?? throw Unavailable();

        if (attempt.LastActivityAt == null
            || !await _context.ToeicAttemptSnapshots.AnyAsync(
                snapshot => snapshot.AttemptId == id && snapshot.SchemaVersion == 1, cancellationToken))
        {
            throw AppException.Conflict("Legacy attempts are not supported by this practice API.",
                ErrorCodes.LegacyAttemptUnsupported);
        }

        return attempt;
    }

    /// <summary>
    /// Runs <paramref name="mutate"/> on a tracked attempt inside the source fence. An expired practice attempt
    /// is deleted (and reported as unavailable) instead of being mutated.
    /// </summary>
    public async Task<T> MutateAsync<T>(
        Guid id,
        Func<ToeicAttempt, PracticeAttemptState, Task<T>> mutate,
        CancellationToken cancellationToken)
    {
        var owner = RequireLearner();
        var initial = await LoadOwnedAsync(id, track: false, cancellationToken);

        var result = await _context.ExecuteAttemptUpdateAsync(owner, SourceId(initial), async () =>
        {
            var attempt = await LoadOwnedAsync(id, track: true, cancellationToken);
            var state = await FenceAsync(owner, SourceId(attempt), cancellationToken);

            if (IsExpired(attempt))
            {
                await DeleteAsync(attempt, state, cancellationToken);
                return (Expired: true, Value: default(T)!);
            }

            return (Expired: false, Value: await mutate(attempt, state));
        }, cancellationToken);

        return result.Expired ? throw Unavailable() : result.Value;
    }

    public async Task<PracticeAttemptState> FenceAsync(Guid owner, Guid sourceId, CancellationToken cancellationToken)
    {
        var state = await _context.PracticeAttemptStates
            .SingleOrDefaultAsync(item => item.UserId == owner && item.PracticeSetId == sourceId, cancellationToken);

        if (state != null)
        {
            return state;
        }

        state = new PracticeAttemptState { UserId = owner, PracticeSetId = sourceId };
        _context.PracticeAttemptStates.Add(state);
        await _context.SaveChangesAsync(cancellationToken);

        return state;
    }

    /// <summary>Deletes only attempt-owned progress; sources and retained media are preserved.</summary>
    public async Task DeleteAsync(ToeicAttempt attempt, PracticeAttemptState state, CancellationToken cancellationToken)
    {
        _context.ToeicAttemptAnswers.RemoveRange(
            await _context.ToeicAttemptAnswers.Where(item => item.AttemptId == attempt.Id).ToListAsync(cancellationToken));
        _context.ToeicAttemptQuestions.RemoveRange(
            await _context.ToeicAttemptQuestions.Where(item => item.AttemptId == attempt.Id).ToListAsync(cancellationToken));
        _context.ToeicAttemptSnapshots.RemoveRange(
            await _context.ToeicAttemptSnapshots.Where(item => item.AttemptId == attempt.Id).ToListAsync(cancellationToken));
        _context.ToeicAttempts.Remove(attempt);
        state.Generation++;
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Keeps the attempt (and its answers) as history but ends it, e.g. before a restart or retake.</summary>
    public async Task AbandonAsync(ToeicAttempt attempt, PracticeAttemptState state, CancellationToken cancellationToken)
    {
        var now = Now;
        attempt.Status = AttemptStatus.Abandoned;
        attempt.EndTime = now;
        attempt.LastActivityAt = now;
        state.Generation++;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
