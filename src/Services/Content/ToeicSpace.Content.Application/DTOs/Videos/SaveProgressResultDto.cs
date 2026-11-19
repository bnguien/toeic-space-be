namespace ToeicSpace.Content.Application.DTOs.Videos;

public sealed record SaveProgressResultDto(string VideoId, int Position, int Percent, bool Completed);
