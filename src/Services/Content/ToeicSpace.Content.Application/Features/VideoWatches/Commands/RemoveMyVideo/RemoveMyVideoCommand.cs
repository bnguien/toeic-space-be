namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.RemoveMyVideo;

/// <summary>Removes a video from my list (soft delete; reopening it brings it back).</summary>
public sealed record RemoveMyVideoCommand(Guid Id, Guid LearnerId) : IRequest;
