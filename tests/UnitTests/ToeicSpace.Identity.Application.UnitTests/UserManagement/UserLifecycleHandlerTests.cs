using ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserRole;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserStatus;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.DeleteUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.RestoreUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.RevokeUserSessions;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.UnitTests.UserManagement;

public sealed class UserLifecycleHandlerTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Changing_the_role_ends_every_session_and_the_next_sign_in_carries_the_new_role()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        var student = await context.AddUserAsync("student@toeicspace.vn");
        var session = await context.SignInAsync(student.Email);

        var result = await context.RoleHandler().Handle(
            new ChangeUserRoleCommand(admin.Id, student.Id, "teacher"), None);

        result.Role.Should().Be("Teacher");
        result.ActiveSessionCount.Should().Be(0);
        await AssertRefreshFailsAsync(context, session.RefreshToken);
        (await context.SignInAsync(student.Email)).User.Role.Should().Be("Teacher");
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_demoted_suspended_or_deleted()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var actor = await context.AddUserAsync("actor@toeicspace.vn", UserRole.Admin, UserStatus.Suspended);
        var lastAdmin = await context.AddUserAsync("last@toeicspace.vn", UserRole.Admin);

        // The suspended actor does not count as a remaining administrator.
        await AssertFailsAsync(
            () => context.RoleHandler().Handle(new ChangeUserRoleCommand(actor.Id, lastAdmin.Id, "User"), None),
            ErrorType.Conflict, ErrorCodes.LastAdministrator);
        await AssertFailsAsync(
            () => context.StatusHandler().Handle(new ChangeUserStatusCommand(actor.Id, lastAdmin.Id, "Suspended"), None),
            ErrorType.Conflict, ErrorCodes.LastAdministrator);
        await AssertFailsAsync(
            () => context.DeleteHandler().Handle(new DeleteUserCommand(actor.Id, lastAdmin.Id), None),
            ErrorType.Conflict, ErrorCodes.LastAdministrator);

        var unchanged = await context.ReloadAsync(lastAdmin.Id);
        unchanged.Role.Should().Be(UserRole.Admin);
        unchanged.Status.Should().Be(UserStatus.Active);
        unchanged.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task An_administrator_can_be_demoted_while_another_one_remains()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var actor = await context.AddUserAsync("actor@toeicspace.vn", UserRole.Admin);
        var other = await context.AddUserAsync("other@toeicspace.vn", UserRole.Admin);

        var result = await context.RoleHandler().Handle(
            new ChangeUserRoleCommand(actor.Id, other.Id, "User"), None);

        result.Role.Should().Be("User");
    }

    [Fact]
    public async Task Administrators_cannot_change_their_own_account()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        await context.AddUserAsync("second@toeicspace.vn", UserRole.Admin);

        await AssertFailsAsync(
            () => context.RoleHandler().Handle(new ChangeUserRoleCommand(admin.Id, admin.Id, "User"), None),
            ErrorType.Forbidden, ErrorCodes.CannotModifySelf);
        await AssertFailsAsync(
            () => context.StatusHandler().Handle(new ChangeUserStatusCommand(admin.Id, admin.Id, "Suspended"), None),
            ErrorType.Forbidden, ErrorCodes.CannotModifySelf);
        await AssertFailsAsync(
            () => context.DeleteHandler().Handle(new DeleteUserCommand(admin.Id, admin.Id), None),
            ErrorType.Forbidden, ErrorCodes.CannotModifySelf);
        await AssertFailsAsync(
            () => context.RevokeHandler().Handle(new RevokeUserSessionsCommand(admin.Id, admin.Id), None),
            ErrorType.Forbidden, ErrorCodes.CannotModifySelf);
    }

    [Fact]
    public async Task Suspending_ends_the_sessions_and_blocks_sign_in_until_reactivated()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        var teacher = await context.AddUserAsync("teacher@toeicspace.vn", UserRole.Teacher);
        var session = await context.SignInAsync(teacher.Email);

        var suspended = await context.StatusHandler().Handle(
            new ChangeUserStatusCommand(admin.Id, teacher.Id, "Suspended"), None);

        suspended.Status.Should().Be("Suspended");
        await AssertRefreshFailsAsync(context, session.RefreshToken);
        await AssertFailsAsync(
            () => context.SignInAsync(teacher.Email), ErrorType.Forbidden, ErrorCodes.AccountLocked);

        await context.StatusHandler().Handle(
            new ChangeUserStatusCommand(admin.Id, teacher.Id, "Active"), None);

        (await context.SignInAsync(teacher.Email)).User.Id.Should().Be(teacher.Id);
    }

    [Fact]
    public async Task An_unverified_account_cannot_be_activated_or_suspended()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        var pending = await context.AddUserAsync(
            "pending@toeicspace.vn", status: UserStatus.Inactive, emailVerified: false);

        await AssertFailsAsync(
            () => context.StatusHandler().Handle(new ChangeUserStatusCommand(admin.Id, pending.Id, "Active"), None),
            ErrorType.Conflict, ErrorCodes.UserNotVerified);

        (await context.ReloadAsync(pending.Id)).Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public async Task Deleting_blocks_sign_in_and_restoring_keeps_role_and_status()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        var teacher = await context.AddUserAsync("teacher@toeicspace.vn", UserRole.Teacher);
        var session = await context.SignInAsync(teacher.Email);

        await context.DeleteHandler().Handle(new DeleteUserCommand(admin.Id, teacher.Id), None);

        (await context.ReloadAsync(teacher.Id)).DeletedAt.Should().Be(context.Identity.Time.Now.UtcDateTime);
        await AssertRefreshFailsAsync(context, session.RefreshToken);
        await AssertFailsAsync(
            () => context.SignInAsync(teacher.Email), ErrorType.Unauthenticated, ErrorCodes.InvalidCredentials);

        var restored = await context.RestoreHandler().Handle(new RestoreUserCommand(admin.Id, teacher.Id), None);

        restored.DeletedAt.Should().BeNull();
        restored.Role.Should().Be("Teacher");
        restored.Status.Should().Be("Active");
        (await context.SignInAsync(teacher.Email)).User.Id.Should().Be(teacher.Id);
    }

    [Fact]
    public async Task Deleted_accounts_only_accept_a_restore()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        var deleted = await context.AddUserAsync(
            "deleted@toeicspace.vn", deletedAt: context.Identity.Time.Now.UtcDateTime);
        var active = await context.AddUserAsync("active@toeicspace.vn");

        await AssertFailsAsync(
            () => context.RoleHandler().Handle(new ChangeUserRoleCommand(admin.Id, deleted.Id, "Teacher"), None),
            ErrorType.Conflict, ErrorCodes.UserDeleted);
        await AssertFailsAsync(
            () => context.StatusHandler().Handle(new ChangeUserStatusCommand(admin.Id, deleted.Id, "Suspended"), None),
            ErrorType.Conflict, ErrorCodes.UserDeleted);
        await AssertFailsAsync(
            () => context.DeleteHandler().Handle(new DeleteUserCommand(admin.Id, deleted.Id), None),
            ErrorType.Conflict, ErrorCodes.UserDeleted);
        await AssertFailsAsync(
            () => context.RestoreHandler().Handle(new RestoreUserCommand(admin.Id, active.Id), None),
            ErrorType.Conflict, ErrorCodes.UserNotDeleted);
    }

    [Fact]
    public async Task Revoking_sessions_signs_the_user_out_everywhere()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        var student = await context.AddUserAsync("student@toeicspace.vn");
        var laptop = await context.SignInAsync(student.Email);
        var phone = await context.SignInAsync(student.Email);

        (await context.Reader.ReadAsync(student, None)).ActiveSessionCount.Should().Be(2);

        await context.RevokeHandler().Handle(new RevokeUserSessionsCommand(admin.Id, student.Id), None);

        await AssertRefreshFailsAsync(context, laptop.RefreshToken);
        await AssertRefreshFailsAsync(context, phone.RefreshToken);
        (await context.Reader.ReadAsync(student, None)).ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public async Task Unknown_users_are_reported_as_not_found()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);

        await AssertFailsAsync(
            () => context.RoleHandler().Handle(new ChangeUserRoleCommand(admin.Id, Guid.NewGuid(), "User"), None),
            ErrorType.NotFound, ErrorCodes.UserNotFound);
    }

    private static async Task AssertRefreshFailsAsync(UserManagementTestContext context, string refreshToken)
        => await AssertFailsAsync(
            () => context.RefreshAsync(refreshToken), ErrorType.Unauthenticated, ErrorCodes.TokenRevoked);

    private static async Task AssertFailsAsync(Func<Task> action, ErrorType type, string code)
    {
        var result = await FluentActions.Awaiting(action).Should().ThrowAsync<AppException>();

        result.Which.Type.Should().Be(type);
        result.Which.Code.Should().Be(code);
    }
}
