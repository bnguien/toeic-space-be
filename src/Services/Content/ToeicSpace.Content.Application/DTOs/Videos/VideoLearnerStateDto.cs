using System.Text.Json;

namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>Reader settings (object, null = defaults), listen mode, path track ("" = not chosen) and last change time.</summary>
public sealed record VideoLearnerStateDto(JsonElement? ReaderSettings, string ListenMode, string PathTrack,
    string UpdatedAt);
