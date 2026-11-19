namespace ToeicSpace.Content.Application.DTOs.Videos;

public sealed record MyVideosDto(IReadOnlyList<WatchedVideoDto> Mine, IReadOnlyList<WatchedVideoDto> Shared);
