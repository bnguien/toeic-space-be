namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>A topic row with its first page and its video count per listening track.</summary>
public sealed record CatalogTopicDto(string Slug, string Title, IReadOnlyDictionary<string, int> Tracks,
    IReadOnlyList<CatalogVideoDto> Videos, bool HasMore, int NextOffset);
