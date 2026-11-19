using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

public sealed record SavedWordDto(Guid Id, Guid? FolderId, string? Note, DateTime SavedAt, bool IsCustom, WordDto Word,
    ProgressDto Progress);
