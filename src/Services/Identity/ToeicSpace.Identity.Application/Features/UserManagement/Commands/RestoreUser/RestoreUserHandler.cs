using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.RestoreUser;

public sealed class RestoreUserHandler : IRequestHandler<RestoreUserCommand, ManagedUserDetailResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ManagedUserReader _reader;
    private readonly ILogger<RestoreUserHandler> _logger;

    public RestoreUserHandler(
        IUserRepository userRepository,
        ManagedUserReader reader,
        ILogger<RestoreUserHandler> logger)
    {
        _userRepository = userRepository;
        _reader = reader;
        _logger = logger;
    }

    public async Task<ManagedUserDetailResult> Handle(
        RestoreUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await UserAdministrationRules.GetExistingAsync(
            _userRepository,
            request.UserId,
            cancellationToken);

        if (user.DeletedAt is null)
        {
            throw AppException.Conflict(
                "The account is not deleted.",
                ErrorCodes.UserNotDeleted);
        }

        user.DeletedAt = null;
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Administrator {ActorId} restored user {UserId}",
            request.ActorId,
            user.Id);

        return await _reader.ReadAsync(user, cancellationToken);
    }
}
