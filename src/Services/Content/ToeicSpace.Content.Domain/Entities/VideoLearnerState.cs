using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// Per-learner video preferences (table <c>VideoLearnerStates</c>, one row per learner, absent = defaults): reader
/// settings JSON, listen mode and the chosen listening path track. Studychill kept these in localStorage only.
/// </summary>
public sealed class VideoLearnerState
{
    public const int ReaderSettingsMaxLength = 4000;
    public const int ListenModeMaxLength = 16;

    public Guid LearnerId { get; set; }
    /// <summary>Reader settings object (camelCase keys, see docs/video.md); null = defaults.</summary>
    public string? ReaderSettings { get; set; }
    /// <summary><c>sentence</c>, <c>flow</c>, <c>cloze</c> or <c>rewrite</c>; empty = default.</summary>
    public string ListenMode { get; set; } = string.Empty;
    public VideoTrack PathTrack { get; set; } = VideoTrack.None;
    public DateTime UpdatedAt { get; set; }
}
