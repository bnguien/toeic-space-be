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
