using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

/// <summary>The updated saved word (same fields as <see cref="SavedWordDto"/>) plus the fields that were filled.</summary>
public sealed record AiFillResultDto(Guid Id, Guid? FolderId, string? Note, DateTime SavedAt, bool IsCustom, WordDto Word,
    ProgressDto Progress, IReadOnlyList<string> Filled)
{
    public static AiFillResultDto From(SavedWordDto x, IReadOnlyList<string> filled) =>
        new(x.Id, x.FolderId, x.Note, x.SavedAt, x.IsCustom, x.Word, x.Progress, filled);
}
