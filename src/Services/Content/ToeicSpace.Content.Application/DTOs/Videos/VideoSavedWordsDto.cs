namespace ToeicSpace.Content.Application.DTOs.Videos;

public sealed record VideoSavedWordsDto(int Total, IReadOnlyList<VideoSavedWordDto> Items);
