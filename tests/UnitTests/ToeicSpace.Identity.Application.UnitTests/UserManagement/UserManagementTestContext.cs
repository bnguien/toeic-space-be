using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Identity.Application.Features.Login.Commands;
using ToeicSpace.Identity.Application.Features.RefreshSession.Commands;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserRole;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserStatus;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.CreateUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.DeleteUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.RestoreUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.RevokeUserSessions;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.UpdateUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserById;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUsers;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserStatistics;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Application.UnitTests.UserManagement;

/// <summary>
/// The user management handlers wired to the real repositories of <see cref="IdentityTestContext"/>.
/// </summary>
public sealed class UserManagementTestContext : IAsyncDisposable
{
    private UserManagementTestContext(IdentityTestContext identity)
    {
        Identity = identity;
        Reader = new ManagedUserReader(identity.RefreshTokens, identity.Time);
    }

    public IdentityTestContext Identity { get; }

    public ManagedUserReader Reader { get; }

    public static async Task<UserManagementTestContext> CreateAsync()
        => new(await IdentityTestContext.CreateAsync());

    public async Task<User> AddUserAsync(
        string email,
        UserRole role = UserRole.User,
        UserStatus status = UserStatus.Active,
        bool emailVerified = true,
        string? fullName = null,
        string? phone = null,
        DateTime? createdAt = null,
        DateTime? deletedAt = null)
    {
        var now = Identity.Time.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName ?? email.Split('@')[0],
            Email = email,
            Phone = phone,
            PasswordHash = Identity.PasswordHasher.Hash(IdentityTestContext.Password),
            EmailVerifiedAt = emailVerified ? now : null,
            Role = role,
            Status = status,
            CreatedAt = createdAt ?? now,
            DeletedAt = deletedAt
        };

        Identity.Db.Users.Add(user);
        await Identity.Db.SaveChangesAsync();
        Identity.Db.ChangeTracker.Clear();

        return user;
    }

    public Task<AuthSession> SignInAsync(string email, string? password = null)
        => Identity.LoginHandler().Handle(
            new LoginCommand(email, password ?? IdentityTestContext.Password),
            CancellationToken.None);

    public Task<AuthSession> RefreshAsync(string refreshToken)
        => Identity.RefreshHandler().Handle(
            new RefreshSessionCommand(refreshToken),
            CancellationToken.None);

    public async Task<User> ReloadAsync(Guid userId)
    {
        Identity.Db.ChangeTracker.Clear();
        return (await Identity.Users.GetByIdAsync(userId, CancellationToken.None))!;
    }

    public CreateUserHandler CreateHandler()
        => new(Identity.Users, Identity.PasswordHasher, new TemporaryPasswordGenerator(), Reader,
            Identity.Time, NullLogger<CreateUserHandler>.Instance);

    public UpdateUserHandler UpdateHandler()
        => new(Identity.Users, Reader, NullLogger<UpdateUserHandler>.Instance);

    public ChangeUserRoleHandler RoleHandler()
        => new(Identity.Users, Identity.RefreshTokens, Reader, NullLogger<ChangeUserRoleHandler>.Instance);

    public ChangeUserStatusHandler StatusHandler()
        => new(Identity.Users, Identity.RefreshTokens, Reader, NullLogger<ChangeUserStatusHandler>.Instance);

    public RevokeUserSessionsHandler RevokeHandler()
        => new(Identity.Users, Identity.RefreshTokens, NullLogger<RevokeUserSessionsHandler>.Instance);

    public DeleteUserHandler DeleteHandler()
        => new(Identity.Users, Identity.RefreshTokens, Identity.Time, NullLogger<DeleteUserHandler>.Instance);

    public RestoreUserHandler RestoreHandler()
        => new(Identity.Users, Reader, NullLogger<RestoreUserHandler>.Instance);

    public GetUsersHandler ListHandler()
        => new(Identity.Users);

    public GetUserByIdHandler DetailHandler()
        => new(Identity.Users, Reader);

    public GetUserStatisticsHandler StatisticsHandler()
        => new(Identity.Users, Identity.Time);

    public ValueTask DisposeAsync() => Identity.DisposeAsync();
}
