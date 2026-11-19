using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services;

/// <summary>Rules shared by the library use cases: folder ownership, custom word fields and DTO building.</summary>
public sealed class LibraryWordService(ILearnerLibraryRepository library, ILearnerProgressRepository progress,
    TimeProvider clock)
{
    /// <summary>Throws <c>vocabulary_folder_not_found</c> unless the folder (when given) belongs to the learner.</summary>
    public async Task EnsureFolderAsync(Guid learnerId, Guid? folderId, CancellationToken ct)
    {
        if (folderId is { } id && !await library.FolderExistsAsync(learnerId, id, ct))
            throw VocabularyErrors.FolderNotFound();
    }

    /// <summary>Copies a custom word's fields after checking per-owner uniqueness.</summary>
    public async Task ApplyCustomAsync(VocabularyWord word, Guid learnerId, CustomWordInput input, DateTime now,
        CancellationToken ct)
    {
        var normalized = VocabularyWord.Normalize(input.Headword);
        if (await library.CustomWordExistsAsync(learnerId, normalized, input.PartOfSpeech,
                word.CreatedAt == default ? null : word.Id, ct))
            throw VocabularyErrors.CustomWordExists();
        word.OwnerKey = learnerId;
        word.Headword = input.Headword.Trim();
        word.NormalizedHeadword = normalized;
        word.PartOfSpeech = input.PartOfSpeech;
        word.Phonetic = input.Phonetic?.Trim() ?? string.Empty;
        word.MeaningVi = input.MeaningVi.Trim();
        word.DefinitionEn = Clean(input.DefinitionEn);
        word.Example = Clean(input.Example);
        word.ExampleVi = Clean(input.ExampleVi);
        word.UpdatedAt = now;
        if (word.CreatedAt == default) word.CreatedAt = now;
    }

    public async Task<SavedWordDto> ToDtoAsync(SavedWord saved, int tzOffset, CancellationToken ct)
    {
        var today = SpacedRepetitionScheduler.LocalDay(clock.GetUtcNow().UtcDateTime, tzOffset);
        var row = (await progress.GetForWordsAsync(saved.LearnerId, [saved.WordId], ct)).FirstOrDefault();
        return saved.ToDto(row, today);
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
