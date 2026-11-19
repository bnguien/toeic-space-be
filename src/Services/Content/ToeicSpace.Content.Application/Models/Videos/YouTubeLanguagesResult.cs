using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>
/// Subtitle languages of a video as YouTube lists them. <see cref="Error"/> is set when YouTube failed;
/// <see cref="Blocked"/> = rate limit / bot check (temporary), <see cref="Permanent"/> = removed, private or unplayable.
/// </summary>
public sealed record YouTubeLanguagesResult(IReadOnlyList<VideoLanguage> Manual, IReadOnlyList<VideoLanguage> Auto,
    string Title, int Duration, string Language, string? Error = null, bool Blocked = false, bool Permanent = false)
{
    public static YouTubeLanguagesResult Failed(string error, bool blocked, bool permanent) =>
        new([], [], string.Empty, 0, string.Empty, error, blocked, permanent);
}
