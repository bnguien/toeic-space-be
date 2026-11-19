using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoLearnerStates.Queries.GetVideoLearnerState;

/// <summary>The learner's reader settings, listen mode and path track (defaults when never saved).</summary>
public sealed record GetVideoLearnerStateQuery(Guid LearnerId) : IRequest<VideoLearnerStateDto>;
