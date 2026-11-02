namespace ToeicSpace.Identity.Application.DTOs.Registration;

public sealed record ResendVerificationResult(
    Guid UserId,
    string ChallengeId);
