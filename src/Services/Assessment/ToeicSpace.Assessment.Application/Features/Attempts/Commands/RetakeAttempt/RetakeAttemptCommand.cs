using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.RetakeAttempt;

/// <summary>
/// Starts a new Practice attempt from a completed one, with the same source and the original snapshot content.
/// Wrong = wrong and unanswered questions (whole passage groups kept). Any in-progress attempt on the same source
/// is abandoned first.
/// </summary>
public sealed record RetakeAttemptCommand(
    Guid AttemptId,
    RetakeScope? Scope) : IRequest<AttemptDto>;
