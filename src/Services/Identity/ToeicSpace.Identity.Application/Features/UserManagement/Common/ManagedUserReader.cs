using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Common;

/// <summary>
/// Builds the detail view, which adds session information to the stored profile.
/// </summary>
public sealed class ManagedUserReader
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly TimeProvider _timeProvider;

    public ManagedUserReader(
        IRefreshTokenRepository refreshTokenRepository,
        TimeProvider timeProvider)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ManagedUserDetailResult> ReadAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var activeSessions = await _refreshTokenRepository.CountActiveSessionsAsync(
            user.Id,
            now,
            cancellationToken);
        var lastActiveAt = await _refreshTokenRepository.GetLastActivityAsync(
            user.Id,
            cancellationToken);

        return user.ToManagedUserDetailResult(activeSessions, lastActiveAt);
    }
}
