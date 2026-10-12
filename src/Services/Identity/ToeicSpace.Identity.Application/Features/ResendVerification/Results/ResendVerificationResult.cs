namespace ToeicSpace.Identity.Application.Features.ResendVerification.Results;

public sealed record ResendVerificationResult(
    Guid UserId,
    string ChallengeId);
