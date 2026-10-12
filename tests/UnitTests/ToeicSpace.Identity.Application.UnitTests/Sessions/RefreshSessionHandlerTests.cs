using ToeicSpace.Identity.Application.Features.Login.Commands;
using ToeicSpace.Identity.Application.Features.Logout.Commands;
using ToeicSpace.Identity.Application.Features.RefreshSession.Commands;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.UnitTests.Sessions;

public sealed class RefreshSessionHandlerTests
{
    [Fact]
    public async Task Refresh_rotates_the_token_without_extending_the_session()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var (user, login) = await SignInAsync(context);

        context.Time.Advance(TimeSpan.FromHours(2));
        var refreshed = await Refresh(context, login.RefreshToken);

        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);
        refreshed.AccessToken.Should().NotBe(login.AccessToken);
        refreshed.RefreshTokenExpiresAt.Should().Be(login.RefreshTokenExpiresAt);
        refreshed.AccessTokenExpiresAt.Should().Be(context.Time.Now.AddMinutes(10));

        var rows = await context.RefreshTokenRowsAsync(user.Id);
        rows.Should().HaveCount(2);
        rows[0].UsedAt.Should().NotBeNull();
        rows[1].UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task Reusing_a_rotated_token_ends_every_session()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var (user, login) = await SignInAsync(context);
        var otherDevice = await context.LoginHandler().Handle(
            new LoginCommand(user.Email, IdentityTestContext.Password),
            CancellationToken.None);

        var stolenCopy = login.RefreshToken;
        var attackerSession = await Refresh(context, stolenCopy);

        // The legitimate client presents the token the attacker already rotated.
        await AssertSessionEndedAsync(context, stolenCopy, ErrorCodes.TokenRevoked);

        await AssertSessionEndedAsync(context, attackerSession.RefreshToken, ErrorCodes.TokenRevoked);
        await AssertSessionEndedAsync(context, otherDevice.RefreshToken, ErrorCodes.TokenRevoked);
        (await context.RefreshTokenRowsAsync(user.Id)).Should().OnlyContain(token => token.IsRevoked);
    }

    [Fact]
    public async Task Session_expires_after_its_absolute_lifetime()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var (_, login) = await SignInAsync(context);
        var token = login.RefreshToken;

        // Stay active every 20 hours, which never hits the idle timeout.
        for (var refresh = 0; refresh < 10; refresh++)
        {
            context.Time.Advance(TimeSpan.FromHours(20));

            if (context.Time.Now >= login.RefreshTokenExpiresAt)
            {
                await AssertSessionEndedAsync(context, token, ErrorCodes.TokenExpired);
                return;
            }

            token = (await Refresh(context, token)).RefreshToken;
        }

        throw new Xunit.Sdk.XunitException("The session never expired.");
    }

    [Fact]
    public async Task Idle_session_must_sign_in_again()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var (_, login) = await SignInAsync(context);

        context.Time.Advance(TimeSpan.FromHours(25));

        await AssertSessionEndedAsync(context, login.RefreshToken, ErrorCodes.TokenExpired);
    }

    [Theory]
    [InlineData(UserStatus.Suspended, UserRole.Admin)]
    [InlineData(UserStatus.Inactive, UserRole.Teacher)]
    public async Task Disabled_account_cannot_refresh(UserStatus newStatus, UserRole role)
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var (user, login) = await SignInAsync(context, role);

        var stored = await context.Db.Users.FindAsync(user.Id);
        stored!.Status = newStatus;
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();

        await AssertSessionEndedAsync(context, login.RefreshToken, ErrorCodes.TokenRevoked);
        (await context.RefreshTokenRowsAsync(user.Id)).Should().OnlyContain(token => token.IsRevoked);
    }

    [Fact]
    public async Task Role_change_is_reflected_in_the_next_access_token()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var (user, login) = await SignInAsync(context, UserRole.Teacher);

        var stored = await context.Db.Users.FindAsync(user.Id);
        stored!.Role = UserRole.User;
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();

        var refreshed = await Refresh(context, login.RefreshToken);

        refreshed.User.Role.Should().Be("User");
    }

    [Fact]
    public async Task Unknown_or_revoked_tokens_are_rejected()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var (_, login) = await SignInAsync(context);

        await AssertSessionEndedAsync(context, context.TokenGenerator.Generate(), ErrorCodes.TokenInvalid);

        await context.LogoutHandler().Handle(new LogoutCommand(login.RefreshToken), CancellationToken.None);

        await AssertSessionEndedAsync(context, login.RefreshToken, ErrorCodes.TokenRevoked);
    }

    [Fact]
    public async Task Logout_ignores_missing_and_unknown_tokens()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var handler = context.LogoutHandler();

        await handler.Handle(new LogoutCommand(null), CancellationToken.None);
        await handler.Handle(new LogoutCommand(new string('x', 5000)), CancellationToken.None);
        await handler.Handle(new LogoutCommand(context.TokenGenerator.Generate()), CancellationToken.None);
    }

    private static async Task<(ToeicSpace.Identity.Domain.Entities.User User, AuthSession Session)> SignInAsync(
        IdentityTestContext context,
        UserRole role = UserRole.Admin)
    {
        var user = await context.AddUserAsync(role: role);
        var session = await context.LoginHandler().Handle(
            new LoginCommand(user.Email, IdentityTestContext.Password),
            CancellationToken.None);

        return (user, session);
    }

    private static Task<AuthSession> Refresh(IdentityTestContext context, string refreshToken)
        => context.RefreshHandler().Handle(new RefreshSessionCommand(refreshToken), CancellationToken.None);

    private static async Task AssertSessionEndedAsync(
        IdentityTestContext context,
        string refreshToken,
        string expectedCode)
    {
        var result = await FluentActions
            .Awaiting(() => Refresh(context, refreshToken))
            .Should().ThrowAsync<AppException>();

        result.Which.Type.Should().Be(ErrorType.Unauthenticated);
        result.Which.Code.Should().Be(expectedCode);
    }
}
