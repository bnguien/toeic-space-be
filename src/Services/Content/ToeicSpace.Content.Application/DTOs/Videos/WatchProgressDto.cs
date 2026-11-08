namespace ToeicSpace.Content.Application.DTOs.Videos;

public sealed record WatchProgressDto(string VideoId, string Title, int Duration, int Position, int Percent,
    bool Completed, string UpdatedAt);
