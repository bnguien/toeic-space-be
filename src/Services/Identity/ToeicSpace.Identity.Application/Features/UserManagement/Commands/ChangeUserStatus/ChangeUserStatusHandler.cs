using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserStatus;

public sealed class ChangeUserStatusHandler
    : IRequestHandler<ChangeUserStatusCommand, ManagedUserDetailResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ManagedUserReader _reader;
    private readonly ILogger<ChangeUserStatusHandler> _logger;

    public ChangeUserStatusHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ManagedUserReader reader,
        ILogger<ChangeUserStatusHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _reader = reader;
        _logger = logger;
    }

    public async Task<ManagedUserDetailResult> Handle(
        ChangeUserStatusCommand request,
        CancellationToken cancellationToken)
    {
        UserAdministrationRules.EnsureNotSelf(request.ActorId, request.UserId);
        UserAdministrationRules.TryParseAssignableStatus(request.Status, out var status);

        var updated = await _userRepository.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var user = await UserAdministrationRules.GetExistingAsync(_userRepository, request.UserId, ct);
            UserAdministrationRules.EnsureNotDeleted(user);

            // Activating an unverified account would skip the email ownership check.
            if (user.EmailVerifiedAt is null)
            {
                throw AppException.Conflict(
                    "The account has not verified its email address yet.",
                    ErrorCodes.UserNotVerified);
            }

            if (user.Status == status)
            {
                return user;
            }

            if (status == UserStatus.Suspended)
            {
                await UserAdministrationRules.EnsureAnotherAdministratorRemainsAsync(_userRepository, user, ct);
            }

            user.Status = status;
            await _userRepository.SaveChangesAsync(ct);

            if (status == UserStatus.Suspended)
            {
                await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);
            }

            _logger.LogInformation(
                "Administrator {ActorId} changed the status of user {UserId} to {Status}",
                request.ActorId,
                user.Id,
                status);

            return user;
        }, cancellationToken);

        return await _reader.ReadAsync(updated, cancellationToken);
    }
}
