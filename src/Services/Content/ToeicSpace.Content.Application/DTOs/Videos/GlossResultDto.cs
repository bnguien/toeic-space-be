namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary><c>missing</c> = words still without a Vietnamese meaning.</summary>
public sealed record GlossResultDto(IReadOnlyDictionary<string, GlossEntryDto> Gloss, IReadOnlyList<string> Missing,
    string Level);
