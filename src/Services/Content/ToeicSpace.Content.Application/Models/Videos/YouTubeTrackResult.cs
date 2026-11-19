using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>Cues of one track downloaded from YouTube (empty when the track does not exist or YouTube failed).</summary>
public sealed record YouTubeTrackResult(IReadOnlyList<SubtitleCue> Cues, string? Error = null, bool Blocked = false)
{
    public static readonly YouTubeTrackResult Missing = new([], "track not found");
}
