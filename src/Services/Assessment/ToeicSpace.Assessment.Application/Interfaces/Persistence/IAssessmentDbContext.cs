namespace ToeicSpace.Assessment.Application.Interfaces.Persistence;

public interface IAssessmentDbContext
{
    DbSet<ToeicTest> ToeicTests { get; }

    DbSet<ToeicPassage> ToeicPassages { get; }

    DbSet<ToeicQuestion> ToeicQuestions { get; }

    DbSet<ToeicPracticeSet> ToeicPracticeSets { get; }

    DbSet<ToeicPracticeSetItem> ToeicPracticeSetItems { get; }

    DbSet<ToeicAttempt> ToeicAttempts { get; }

    DbSet<ToeicAttemptAnswer> ToeicAttemptAnswers { get; }
    DbSet<ToeicAttemptSnapshot> ToeicAttemptSnapshots { get; }
    DbSet<ToeicAttemptQuestion> ToeicAttemptQuestions { get; }
    DbSet<PracticeAttemptState> PracticeAttemptStates { get; }

    Task<T> ExecuteAttemptUpdateAsync<T>(Guid userId, Guid setId, Func<Task<T>> update, CancellationToken cancellationToken);
    Task LockAttemptSetAsync(Guid setId, CancellationToken cancellationToken);
    Task LockAttemptTestAsync(Guid testId, CancellationToken cancellationToken);
    Task<T> ExecuteAttemptReadAsync<T>(Func<Task<T>> read, CancellationToken cancellationToken);

    Task<T> ExecutePracticeSetUpdateAsync<T>(
        Guid practiceSetId,
        Func<Task<T>> update,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task LockPracticeContentAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken);

    Task<T> ExecuteQuestionUpdateAsync<T>(Guid? questionId, Guid? passageId, Func<Task<T>> update, CancellationToken cancellationToken, Guid? testId = null);

    Task<T> ExecutePassageUpdateAsync<T>(Guid passageId, Func<Task<T>> update, CancellationToken cancellationToken, Guid? testId = null);

    Task<T> ExecuteTestContentUpdateAsync<T>(Guid testId,
        Func<IReadOnlyCollection<Guid>, IReadOnlyCollection<Guid>, Task<T>> update, CancellationToken cancellationToken);
}
