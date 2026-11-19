using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Queries.GetWatchProgress;

/// <summary>The learner's own watch progress, newest first (≤ 300).</summary>
public sealed record GetWatchProgressQuery(Guid LearnerId) : IRequest<WatchProgressListDto>;
