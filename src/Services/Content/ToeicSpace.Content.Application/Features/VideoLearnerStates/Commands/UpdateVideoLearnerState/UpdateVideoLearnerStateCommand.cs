using System.Text.Json;
using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoLearnerStates.Commands.UpdateVideoLearnerState;

/// <summary>Partial update: only the properties sent (non-null) change.</summary>
public sealed record UpdateVideoLearnerStateCommand(JsonElement? ReaderSettings = null, string? ListenMode = null,
    string? PathTrack = null, [property: JsonIgnore] Guid LearnerId = default) : IRequest<VideoLearnerStateDto>
{
    public static readonly string[] ListenModes = ["sentence", "flow", "cloze", "rewrite"];
}
