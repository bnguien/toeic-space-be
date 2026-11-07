namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary>A pending mistake ("Từ sai"): a word answered Forget and not answered Good/Easy since.</summary>
public sealed record MistakeItemDto(WordDto Word, ProgressDto Progress, Guid? SavedWordId, int MistakeCount,
    DateTime LastMistakeAt);
