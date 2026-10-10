using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Messaging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.ChangePassword.Commands;

public sealed class RequestChangePasswordOtpHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IPasswordResetStore otpStore,
    IIntegrationEventPublisher eventPublisher) : IRequestHandler<RequestChangePasswordOtpCommand>
{
    public async Task Handle(RequestChangePasswordOtpCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        var matches = passwordHasher.Verify(request.CurrentPassword, user?.PasswordHash);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash) || !matches)
        {
            throw AppException.Validation("The account or current password is not valid for a password change.",
                ErrorCodes.InvalidCredentials);
        }

        var requestId = await otpStore.BeginRequestAsync(user.Id.ToString("N"), cancellationToken);
        if (requestId is not null)
        {
            await eventPublisher.PublishAsync(new PasswordChangeRequestedEvent(user.Id, requestId),
                cancellationToken);
        }
    }
}
