namespace ToeicSpace.Identity.Application.DTOs.Registration;

public sealed record RegisterResult(
    Guid UserId,
    string ChallengeId);
