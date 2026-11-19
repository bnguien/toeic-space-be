namespace ToeicSpace.Content.Application.DTOs.Videos;

public sealed record SetVideoPublicResultDto(WatchedVideoDto Video, DuplicateShareDto? Duplicate);
