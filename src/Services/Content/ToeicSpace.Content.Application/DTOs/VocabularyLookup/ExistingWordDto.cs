namespace ToeicSpace.Content.Application.DTOs.VocabularyLookup;

/// <summary>The learner already has this word in the library.</summary>
public sealed record ExistingWordDto(Guid SavedWordId, Guid? FolderId);
