namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>Next page of a topic row.</summary>
public sealed record CatalogTopicPageDto(string Slug, string Title, IReadOnlyList<CatalogVideoDto> Videos, bool HasMore,
    int NextOffset);
