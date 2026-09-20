using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.Application.Interfaces.Caching;

public interface IOtpChallengeStore
{
    Task<string> ReplaceChallengeAsync(
        OtpChallenge challenge,
        CancellationToken cancellationToken);

    Task<TimeSpan?> TryAcquireResendCooldownAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<OtpChallenge?> GetAsync(
        string challengeId,
        CancellationToken cancellationToken);

    Task<OtpChallenge?> ConsumeAsync(
        string challengeId,
        string otpHash,
        int maximumFailedAttempts,
        CancellationToken cancellationToken);

    Task<long> IncrementFailedAttemptsAsync(
        string challengeId,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string challengeId,
        CancellationToken cancellationToken);
}
