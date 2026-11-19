using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.SaveWatchProgress;

/// <summary>Current position and duration in seconds; the percent only grows, ≥ 90 % completes the video.</summary>
public sealed record SaveWatchProgressCommand(double Position, double Duration,
    [property: JsonIgnore] string VideoId = "", [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<SaveProgressResultDto>;
