using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface ILearnerProgressRepository
{
    /// <summary>Tracked progress row of one word, or null for a new word.</summary>
    Task<LearnerWordProgress?> GetForUpdateAsync(Guid learnerId, Guid wordId, CancellationToken ct);
    Task<IReadOnlyList<LearnerWordProgress>> GetForWordsAsync(Guid learnerId, IReadOnlyCollection<Guid> wordIds,
        CancellationToken ct);
    Task<IReadOnlyList<LearnerWordProgress>> GetAllAsync(Guid learnerId, CancellationToken ct);
    /// <summary>Due rows ordered by due date, then oldest review.</summary>
    Task<IReadOnlyList<LearnerWordProgress>> GetDueAsync(Guid learnerId, DateOnly localToday, int limit,
        CancellationToken ct);
    Task<WordReviewLog?> GetReviewLogAsync(Guid learnerId, Guid clientEventId, CancellationToken ct);
    /// <summary>Distinct local days with at least one review, newest first.</summary>
    Task<IReadOnlyList<DateOnly>> GetReviewDaysAsync(Guid learnerId, CancellationToken ct);
    Task<int> CountReviewsOnAsync(Guid learnerId, DateOnly localDay, CancellationToken ct);
    void Add(LearnerWordProgress progress);
    void AddReviewLog(WordReviewLog log);
    /// <summary>Marks every progress row and review log of a word for deletion (used when a custom word is removed).</summary>
    Task RemoveAllForWordAsync(Guid wordId, CancellationToken ct);
}
