namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>A card of the learner's own or the shared videos; <c>shareCount</c> = learners sharing this video.</summary>
public sealed record WatchedVideoDto(Guid Id, string VideoId, string Title, int Duration, string Language, bool IsPublic,
    int OpenCount, bool Mine, string UserName, string UserAvatar, int ShareCount, string Thumbnail, string LastOpenedAt);
