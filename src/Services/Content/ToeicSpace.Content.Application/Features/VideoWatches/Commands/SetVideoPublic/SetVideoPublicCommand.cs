using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.SetVideoPublic;

/// <summary>
/// Shares one of my videos publicly (or stops). <see cref="DisplayName"/>/<see cref="AvatarUrl"/> are the sharer card
/// snapshot sent by the client (the access token carries no name).
/// </summary>
public sealed record SetVideoPublicCommand(bool IsPublic, string? DisplayName = null, string? AvatarUrl = null,
    [property: JsonIgnore] Guid Id = default, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<SetVideoPublicResultDto>;
