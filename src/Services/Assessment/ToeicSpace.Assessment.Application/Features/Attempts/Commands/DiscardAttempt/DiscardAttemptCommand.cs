namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.DiscardAttempt;

/// <summary>Permanently deletes an in-progress attempt and its progress (sources and media are kept).</summary>
public sealed record DiscardAttemptCommand(Guid AttemptId) : IRequest;
