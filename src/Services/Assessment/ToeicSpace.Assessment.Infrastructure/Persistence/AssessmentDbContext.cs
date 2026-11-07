using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Domain.Entities;
using ToeicSpace.Assessment.Domain.Exceptions;
using ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;

namespace ToeicSpace.Assessment.Infrastructure.Persistence;

public class AssessmentDbContext : DbContext, IAssessmentDbContext
{
    public AssessmentDbContext(DbContextOptions<AssessmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<ToeicTest> ToeicTests => Set<ToeicTest>();

    public DbSet<ToeicPassage> ToeicPassages => Set<ToeicPassage>();

    public DbSet<ToeicQuestion> ToeicQuestions => Set<ToeicQuestion>();

    public DbSet<ToeicPracticeSet> ToeicPracticeSets => Set<ToeicPracticeSet>();

    public DbSet<ToeicPracticeSetItem> ToeicPracticeSetItems => Set<ToeicPracticeSetItem>();

    public DbSet<ToeicAttempt> ToeicAttempts => Set<ToeicAttempt>();

    public DbSet<ToeicAttemptAnswer> ToeicAttemptAnswers => Set<ToeicAttemptAnswer>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task<T> ExecutePracticeSetUpdateAsync<T>(
        Guid practiceSetId,
        Func<Task<T>> update,
        CancellationToken cancellationToken)
        => await ExecuteContentUpdateAsync(async () =>
        {
            await ToeicPracticeSets.FromSqlInterpolated(
                    $"SELECT * FROM ToeicPracticeSets WHERE Id = {practiceSetId} FOR UPDATE")
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        }, update, cancellationToken);

    // Lock order: set (if any), sorted questions, sorted passages. Bank writers never lock sets.
    // All subsequent guards are nonlocking READ COMMITTED reads, including after waiting for a lock.
    public async Task LockPracticeContentAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken)
    {
        if (!Database.IsRelational()) return;
        var passageIds = await LockQuestionsAsync(questionIds, cancellationToken);
        await LockPassagesAsync(passageIds, cancellationToken);
    }

    private async Task<List<Guid>> LockQuestionsAsync(IEnumerable<Guid> questionIds, CancellationToken cancellationToken)
    {
        var ids = questionIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return [];
        var placeholders = string.Join(", ", ids.Select((_, index) => "{" + index + "}"));
        var rows = await ToeicQuestions.FromSql(System.Runtime.CompilerServices.FormattableStringFactory.Create(
                $"SELECT * FROM ToeicQuestions WHERE Id IN ({placeholders}) ORDER BY Id FOR UPDATE", ids.Cast<object>().ToArray()))
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        return rows.Where(q => q.PassageId.HasValue).Select(q => q.PassageId!.Value).Distinct().ToList();
    }

    public Task<T> ExecuteQuestionUpdateAsync<T>(Guid? questionId, Guid? passageId, Func<Task<T>> update, CancellationToken cancellationToken, Guid? testId = null)
        => ExecuteContentUpdateAsync(async () =>
        {
            await LockTestAsync(testId, cancellationToken);
            var passageIds = new List<Guid>();
            if (passageId.HasValue) passageIds.Add(passageId.Value);
            if (questionId.HasValue)
            {
                var rows = await ToeicQuestions.FromSqlInterpolated(
                        $"SELECT * FROM ToeicQuestions WHERE Id = {questionId.Value} FOR UPDATE")
                    .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
                passageIds.AddRange(rows.Where(q => q.PassageId.HasValue).Select(q => q.PassageId!.Value));
            }
            await LockPassagesAsync(passageIds, cancellationToken);
        }, update, cancellationToken);

    public Task<T> ExecutePassageUpdateAsync<T>(Guid passageId, Func<Task<T>> update, CancellationToken cancellationToken, Guid? testId = null)
        => ExecuteContentUpdateAsync(async () =>
        {
            await LockTestAsync(testId, cancellationToken);
            await LockPassagesAsync([passageId], cancellationToken);
        }, update, cancellationToken);

    public async Task<T> ExecuteTestContentUpdateAsync<T>(Guid testId,
        Func<IReadOnlyCollection<Guid>, IReadOnlyCollection<Guid>, Task<T>> update, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Guid> questionIds = [];
        IReadOnlyCollection<Guid> passageIds = [];
        return await ExecuteContentUpdateAsync(async () =>
        {
            await LockTestAsync(testId, cancellationToken);
            questionIds = await ToeicQuestions.AsNoTracking().Where(q => q.TestId == testId)
                .Select(q => q.Id).ToListAsync(cancellationToken);
            var referencedPassages = await LockQuestionsAsync(questionIds, cancellationToken);
            passageIds = await ToeicPassages.AsNoTracking().Where(p => p.TestId == testId)
                .Select(p => p.Id).ToListAsync(cancellationToken);
            await LockPassagesAsync(passageIds.Concat(referencedPassages), cancellationToken);
            // A new/moved question may have committed before its passage lock was acquired.
            // Abort rather than locking more questions after passages (which reverses lock order).
            if (await ToeicQuestions.AnyAsync(q => q.TestId == testId && !questionIds.Contains(q.Id), cancellationToken)
                || await ToeicPassages.AnyAsync(p => p.TestId == testId && !passageIds.Contains(p.Id), cancellationToken))
                throw AppException.Conflict("Test content changed during deletion. Reload and try again.", ErrorCodes.ConcurrencyConflict);
        }, async () =>
        {
            if (!Database.IsRelational())
            {
                questionIds = await ToeicQuestions.Where(q => q.TestId == testId).Select(q => q.Id).ToListAsync(cancellationToken);
                passageIds = await ToeicPassages.Where(p => p.TestId == testId).Select(p => p.Id).ToListAsync(cancellationToken);
            }
            return await update(questionIds, passageIds);
        }, cancellationToken);
    }

    private async Task LockPassagesAsync(IEnumerable<Guid> passageIds, CancellationToken cancellationToken)
    {
        var ids = passageIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return;
        var placeholders = string.Join(", ", ids.Select((_, index) => "{" + index + "}"));
        await ToeicPassages.FromSql(System.Runtime.CompilerServices.FormattableStringFactory.Create(
                $"SELECT * FROM ToeicPassages WHERE Id IN ({placeholders}) ORDER BY Id FOR UPDATE", ids.Cast<object>().ToArray()))
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
    }

    private async Task LockTestAsync(Guid? testId, CancellationToken cancellationToken)
    {
        if (testId is not { } id) return;
        await ToeicTests.FromSqlInterpolated($"SELECT * FROM ToeicTests WHERE Id = {id} FOR UPDATE")
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        if (!await ToeicTests.AnyAsync(t => t.Id == id, cancellationToken))
            throw AppException.NotFound(nameof(ToeicTest), id);
    }

    private async Task<T> ExecuteContentUpdateAsync<T>(Func<Task> acquireLocks, Func<Task<T>> update, CancellationToken cancellationToken)
    {
        if (!Database.IsRelational())
        {
            return await update();
        }

        // Retry the entire transaction, rather than individual statements, after transient errors.
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(
                System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            await acquireLocks();
            var result = await update();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssessmentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
