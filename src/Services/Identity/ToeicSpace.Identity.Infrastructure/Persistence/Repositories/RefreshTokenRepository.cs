using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _dbContext;

    public RefreshTokenRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
        => _dbContext.UserTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash
                    && token.Type == TokenType.RefreshToken,
                cancellationToken);

    public async Task AddAsync(
        UserToken token,
        CancellationToken cancellationToken)
        => await _dbContext.UserTokens.AddAsync(token, cancellationToken);

    public async Task<bool> TryMarkUsedAsync(
        Guid tokenId,
        DateTime usedAt,
        CancellationToken cancellationToken)
    {
        var updated = await _dbContext.UserTokens
            .Where(token => token.Id == tokenId
                && token.UsedAt == null
                && !token.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.UsedAt, usedAt),
                cancellationToken);

        return updated == 1;
    }

    public Task RevokeAsync(
        Guid tokenId,
        CancellationToken cancellationToken)
        => _dbContext.UserTokens
            .Where(token => token.Id == tokenId && !token.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.IsRevoked, true),
                cancellationToken);

    public Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => _dbContext.UserTokens
            .Where(token => token.UserId == userId
                && token.Type == TokenType.RefreshToken
                && !token.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.IsRevoked, true),
                cancellationToken);

    public Task<int> CountActiveSessionsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
        => _dbContext.UserTokens
            .AsNoTracking()
            .CountAsync(
                token => token.UserId == userId
                    && token.Type == TokenType.RefreshToken
                    && !token.IsRevoked
                    && token.UsedAt == null
                    && token.ExpiresAt > now,
                cancellationToken);

    // Every sign-in and refresh creates a token, so the newest one marks the last activity.
    public Task<DateTime?> GetLastActivityAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => _dbContext.UserTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId && token.Type == TokenType.RefreshToken)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => (DateTime?)token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task EnforceSessionLimitAsync(
        Guid userId,
        int maxActiveSessions,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _dbContext.UserTokens
            .Where(token => token.UserId == userId
                && token.Type == TokenType.RefreshToken
                && token.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        var excessSessionIds = await _dbContext.UserTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId
                && token.Type == TokenType.RefreshToken
                && !token.IsRevoked
                && token.UsedAt == null)
            .OrderByDescending(token => token.CreatedAt)
            .Skip(maxActiveSessions)
            .Select(token => token.Id)
            .ToListAsync(cancellationToken);

        if (excessSessionIds.Count == 0)
        {
            return;
        }

        await _dbContext.UserTokens
            .Where(token => excessSessionIds.Contains(token.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.IsRevoked, true),
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
