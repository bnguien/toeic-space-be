using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>A learner's folders, saved words and private custom words.</summary>
public interface ILearnerLibraryRepository
{
    Task<IReadOnlyList<VocabularyFolder>> GetFoldersAsync(Guid learnerId, CancellationToken ct);
    Task<VocabularyFolder?> GetFolderForUpdateAsync(Guid learnerId, Guid folderId, CancellationToken ct);
    Task<bool> FolderExistsAsync(Guid learnerId, Guid folderId, CancellationToken ct);
    Task<bool> FolderNameExistsAsync(Guid learnerId, string normalizedName, Guid? excludeId, CancellationToken ct);
    Task<int> GetMaxFolderOrderAsync(Guid learnerId, CancellationToken ct);
    void AddFolder(VocabularyFolder folder);
    void RemoveFolder(VocabularyFolder folder);

    /// <summary>All saved words of a learner with their word (no tracking).</summary>
    Task<IReadOnlyList<SavedWord>> GetSavedWordsAsync(Guid learnerId, CancellationToken ct);
    /// <summary>Saved words of one folder (null = unfiled), tracked, with their word.</summary>
    Task<IReadOnlyList<SavedWord>> GetSavedWordsForUpdateAsync(Guid learnerId, Guid? folderId, CancellationToken ct);
    Task<PagedResult<SavedWord>> SearchSavedWordsAsync(Guid learnerId, Guid? folderId, bool unfiledOnly,
        string? search, int page, int pageSize, CancellationToken ct);
    Task<SavedWord?> GetSavedWordForUpdateAsync(Guid learnerId, Guid savedWordId, CancellationToken ct);
    Task<SavedWord?> GetSavedWordByWordForUpdateAsync(Guid learnerId, Guid wordId, CancellationToken ct);
    /// <summary>Saved-word id per word id, for the words the learner has in the library.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetSavedWordIdsAsync(Guid learnerId, IReadOnlyCollection<Guid> wordIds,
        CancellationToken ct);
    Task<int> CountSavedWordsAsync(Guid learnerId, CancellationToken ct);
    void AddSavedWord(SavedWord savedWord);
    void RemoveSavedWord(SavedWord savedWord);

    Task<bool> CustomWordExistsAsync(Guid learnerId, string normalizedHeadword, PartOfSpeech partOfSpeech,
        Guid? excludeWordId, CancellationToken ct);
    /// <summary>The learner's own custom words (no tracking).</summary>
    Task<IReadOnlyList<VocabularyWord>> GetCustomWordsAsync(Guid learnerId, CancellationToken ct);
    /// <summary>Tracked custom words of the learner whose normalized headword is in the list.</summary>
    Task<IReadOnlyList<VocabularyWord>> GetCustomWordsForUpdateAsync(Guid learnerId,
        IReadOnlyCollection<string> normalizedHeadwords, CancellationToken ct);
    /// <summary>Saved words (official or custom, with their word) whose normalized headword matches.</summary>
    Task<IReadOnlyList<SavedWord>> FindSavedByHeadwordAsync(Guid learnerId, string normalizedHeadword,
        CancellationToken ct);
    void AddWord(VocabularyWord word);
    void RemoveWord(VocabularyWord word);
}
