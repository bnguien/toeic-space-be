namespace ToeicSpace.Assessment.Application.Data;

public interface IAssessmentDbContext
{
    DbSet<ToeicTest> ToeicTests { get; }

    DbSet<ToeicPassage> ToeicPassages { get; }

    DbSet<ToeicQuestion> ToeicQuestions { get; }

    DbSet<ToeicPracticeSet> ToeicPracticeSets { get; }

    DbSet<ToeicPracticeSetItem> ToeicPracticeSetItems { get; }

    DbSet<ToeicAttempt> ToeicAttempts { get; }

    DbSet<ToeicAttemptAnswer> ToeicAttemptAnswers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
