namespace ToeicSpace.Identity.Application.Features.VerifyEmail.Results;

public sealed record VerifyEmailResult(
    Guid UserId,
    DateTime EmailVerifiedAt);
