namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>One listening track ordered easy to hard, plus the video count of every track.</summary>
public sealed record CatalogPathDto(string Track, IReadOnlyDictionary<string, int> Tracks,
    IReadOnlyList<CatalogPathVideoDto> Videos);
