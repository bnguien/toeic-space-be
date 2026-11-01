using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>Tracked access used by the vocabulary import (upsert by natural keys).</summary>
public interface IVocabularyImportRepository
{
    /// <summary>Stage by slug with all its sets (active or not) and their word links.</summary>
    Task<VocabularyStage?> GetStageForUpdateAsync(string slug, CancellationToken ct);
    /// <summary>Words whose normalized headword is in the list (all parts of speech).</summary>
    Task<IReadOnlyList<VocabularyWord>> GetWordsForUpdateAsync(IReadOnlyCollection<string> normalizedHeadwords,
        CancellationToken ct);
    void AddStage(VocabularyStage stage);
    void AddSet(VocabularySet set);
    void AddWord(VocabularyWord word);
    void AddSetWord(VocabularySetWord link);
    void RemoveSetWord(VocabularySetWord link);
}
