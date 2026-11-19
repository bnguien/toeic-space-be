namespace ToeicSpace.Identity.Application.DTOs.Registration;

public sealed record VerifyEmailResult(
    Guid UserId,
    DateTime EmailVerifiedAt);
