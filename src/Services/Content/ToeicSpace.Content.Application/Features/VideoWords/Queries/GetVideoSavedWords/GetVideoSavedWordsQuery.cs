using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GetVideoSavedWords;

/// <summary>The learner's library entries (≤ 2,000, newest first) so the reader can mark saved transcript words.</summary>
public sealed record GetVideoSavedWordsQuery(Guid LearnerId) : IRequest<VideoSavedWordsDto>;
