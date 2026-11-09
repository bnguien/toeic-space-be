namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>Token index to correct.</summary>
public sealed record ClozeCheckResultDto(IReadOnlyDictionary<string, bool> Results);
