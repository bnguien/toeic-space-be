using ToeicSpace.Content.Application.Models.Vocabulary;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>Read-only access to active stages, sets and words.</summary>
public interface IVocabularyCatalogRepository
{
    Task<IReadOnlyList<VocabularyStage>> GetStagesAsync(CancellationToken ct);
    Task<bool> StageExistsAsync(Guid stageId, CancellationToken ct);
    /// <summary>Active sets of a stage ordered by <c>Order</c>, with their words.</summary>
    Task<IReadOnlyList<VocabularySet>> GetSetsWithWordsAsync(Guid stageId, CancellationToken ct);
    /// <summary>An active set with its stage and ordered words, or null.</summary>
    Task<VocabularySet?> GetSetDetailAsync(Guid setId, CancellationToken ct);
    /// <summary>Ids of all active sets in roadmap order (stage order, then set order).</summary>
    Task<IReadOnlyList<Guid>> GetOrderedSetIdsAsync(CancellationToken ct);
    /// <summary>All active sets in roadmap order with their word counts.</summary>
    Task<IReadOnlyList<SetOverview>> GetSetOverviewsAsync(CancellationToken ct);
    /// <summary>Word membership of active sets, optionally limited to one stage and/or some words.</summary>
    Task<IReadOnlyList<SetWordMembership>> GetMembershipAsync(Guid? stageId, IReadOnlyCollection<Guid>? wordIds,
        CancellationToken ct);
    /// <summary>Official words plus the private words of <paramref name="learnerId"/> (Guid.Empty = official only).</summary>
    Task<IReadOnlyList<VocabularyWord>> GetWordsAsync(IReadOnlyCollection<Guid> wordIds, Guid learnerId,
        CancellationToken ct);
    /// <summary>True when the word is official or owned by <paramref name="learnerId"/>.</summary>
    Task<bool> IsWordAccessibleAsync(Guid wordId, Guid learnerId, CancellationToken ct);
    /// <summary>Distinct words of the active sets of a stage.</summary>
    Task<IReadOnlyList<VocabularyWord>> GetStageWordsAsync(Guid stageId, CancellationToken ct);
    /// <summary>Ids of every official word with the given part of speech.</summary>
    Task<IReadOnlyList<Guid>> GetOfficialWordIdsAsync(PartOfSpeech partOfSpeech, CancellationToken ct);
}
