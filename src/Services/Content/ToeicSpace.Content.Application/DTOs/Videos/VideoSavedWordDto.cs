namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>A library entry as the reader sees it (transcript word to saved word).</summary>
public sealed record VideoSavedWordDto(Guid SavedWordId, Guid WordId, string Headword, string NormalizedHeadword,
    Guid? FolderId, string? Note, string MeaningVi, string Phonetic, string PartOfSpeech, bool IsCustom, DateTime SavedAt);
