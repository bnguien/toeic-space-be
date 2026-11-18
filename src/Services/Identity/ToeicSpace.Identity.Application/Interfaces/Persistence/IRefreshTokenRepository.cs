using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Application.Interfaces.Persistence;

public interface IRefreshTokenRepository
{
    Task<UserToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task AddAsync(
        UserToken token,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically marks an unused, unrevoked token as used.
    /// Returns false when another request already rotated or revoked it.
    /// </summary>
    Task<bool> TryMarkUsedAsync(
        Guid tokenId,
        DateTime usedAt,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        Guid tokenId,
        CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refresh tokens that can still start a new access token: unused, unrevoked and unexpired.
    /// </summary>
    Task<int> CountActiveSessionsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Time of the latest sign-in or session refresh, or null when the user never signed in.
    /// </summary>
    Task<DateTime?> GetLastActivityAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the user's expired tokens and revokes the oldest active sessions beyond
    /// <paramref name="maxActiveSessions"/>. Used tokens are kept until they expire:
    /// reuse detection needs them for the whole session lifetime.
    /// </summary>
    Task EnforceSessionLimitAsync(
        Guid userId,
        int maxActiveSessions,
        DateTime now,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
