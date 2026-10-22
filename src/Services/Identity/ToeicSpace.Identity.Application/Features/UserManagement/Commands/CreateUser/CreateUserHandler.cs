using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.CreateUser;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITemporaryPasswordGenerator _passwordGenerator;
    private readonly ManagedUserReader _reader;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITemporaryPasswordGenerator passwordGenerator,
        ManagedUserReader reader,
        TimeProvider timeProvider,
        ILogger<CreateUserHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordGenerator = passwordGenerator;
        _reader = reader;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CreateUserResult> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        UserAdministrationRules.TryParseRole(request.Role, out var role);
        var email = IdentityNormalizer.NormalizeEmail(request.Email);
        var phone = IdentityNormalizer.NormalizePhone(request.Phone);

        // Deleted accounts keep their email, so an administrator restores them instead.
        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw EmailTaken();
        }

        if (phone is not null &&
            await _userRepository.PhoneExistsAsync(phone, cancellationToken))
        {
            throw PhoneTaken();
        }

        var temporaryPassword = _passwordGenerator.Generate();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = phone,
            PasswordHash = _passwordHasher.Hash(temporaryPassword),
            EmailVerifiedAt = now,
            Role = role,
            Status = UserStatus.Active,
            CreatedAt = now
        };

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateUserException)
        {
            // Lost a race with a registration using the same email or phone.
            throw EmailTaken();
        }

        _logger.LogInformation(
            "Administrator {ActorId} created user {UserId} with role {Role}",
            request.ActorId,
            user.Id,
            user.Role);

        return new CreateUserResult(
            await _reader.ReadAsync(user, cancellationToken),
            temporaryPassword);
    }

    private static AppException EmailTaken()
        => AppException.Conflict(
            "An account with this email already exists.",
            ErrorCodes.EmailAlreadyExists);

    private static AppException PhoneTaken()
        => AppException.Conflict(
            "An account with this phone number already exists.",
            ErrorCodes.PhoneAlreadyExists);
}
