using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>Per-learner dictation data: sentence progress and the learner state row.</summary>
public interface IDictationProgressRepository
{
    /// <summary>Progress of the learner on active sentences (all of them, or only <paramref name="itemIds"/>).</summary>
    Task<IReadOnlyList<DictationProgress>> GetProgressAsync(Guid learnerId, IReadOnlyCollection<int>? itemIds,
        CancellationToken ct);
    /// <summary>Passed active sentences per course.</summary>
    Task<IReadOnlyDictionary<int, int>> CountPassedByCourseAsync(Guid learnerId, IReadOnlyCollection<int> courseIds,
        CancellationToken ct);
    Task<DictationProgress?> GetForUpdateAsync(Guid learnerId, int itemId, CancellationToken ct);
    void Add(DictationProgress progress);
    Task<DictationLearnerState?> GetStateAsync(Guid learnerId, bool forUpdate, CancellationToken ct);
    void AddState(DictationLearnerState state);
}
