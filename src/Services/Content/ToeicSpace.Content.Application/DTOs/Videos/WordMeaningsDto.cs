namespace ToeicSpace.Content.Application.DTOs.Videos;

public sealed record WordMeaningsDto(IReadOnlyDictionary<string, GlossEntryDto> Items);
