using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.UpdateUser;

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserCommand, ManagedUserDetailResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ManagedUserReader _reader;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(
        IUserRepository userRepository,
        ManagedUserReader reader,
        ILogger<UpdateUserHandler> logger)
    {
        _userRepository = userRepository;
        _reader = reader;
        _logger = logger;
    }

    public async Task<ManagedUserDetailResult> Handle(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await UserAdministrationRules.GetExistingAsync(
            _userRepository,
            request.UserId,
            cancellationToken);
        UserAdministrationRules.EnsureNotDeleted(user);

        var phone = IdentityNormalizer.NormalizePhone(request.Phone);

        if (phone is not null &&
            await _userRepository.PhoneExistsForOtherUserAsync(phone, user.Id, cancellationToken))
        {
            throw PhoneTaken();
        }

        user.FullName = request.FullName.Trim();
        user.Phone = phone;

        try
        {
            await _userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateUserException)
        {
            throw PhoneTaken();
        }

        _logger.LogInformation(
            "Administrator {ActorId} updated the profile of user {UserId}",
            request.ActorId,
            user.Id);

        return await _reader.ReadAsync(user, cancellationToken);
    }

    private static AppException PhoneTaken()
        => AppException.Conflict(
            "An account with this phone number already exists.",
            ErrorCodes.PhoneAlreadyExists);
}
