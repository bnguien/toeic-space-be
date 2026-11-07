namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>The opened video and its subtitle languages; <c>language</c> is a code that exists in the lists.</summary>
public sealed record VideoResolveDto(string VideoId, string Title, int Duration, string Language,
    IReadOnlyList<LangDto> Manual, IReadOnlyList<LangDto> Auto);
