using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Queries.GetMyVideos;

/// <summary>My videos + other learners' public videos (grouped by video, first sharer shown).</summary>
public sealed record GetMyVideosQuery(Guid LearnerId) : IRequest<MyVideosDto>;
