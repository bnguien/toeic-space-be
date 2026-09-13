namespace ToeicSpace.Identity.Application.Features.Register.Results;

public sealed record RegisterResult(
    Guid UserId,
    string ChallengeId);
