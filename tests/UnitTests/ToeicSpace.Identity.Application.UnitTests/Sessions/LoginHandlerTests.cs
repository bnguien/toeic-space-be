using ToeicSpace.Identity.Application.Features.Login.Commands;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.UnitTests.Sessions;

public sealed class LoginHandlerTests
{
    [Fact]
    public async Task Valid_credentials_return_tokens_and_store_only_the_refresh_token_hash()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync();

        var session = await context.LoginHandler().Handle(
            new LoginCommand("  Admin@ToeicSpace.vn ", IdentityTestContext.Password),
            CancellationToken.None);

        session.AccessToken.Should().NotBeNullOrWhiteSpace();
        session.User.Id.Should().Be(user.Id);
        session.User.Role.Should().Be("Admin");
        session.AccessTokenExpiresAt.Should().Be(context.Time.Now.AddMinutes(10));
        session.RefreshTokenExpiresAt.Should().Be(context.Time.Now.AddDays(7));

        var rows = await context.RefreshTokenRowsAsync(user.Id);
        rows.Should().ContainSingle();
        rows[0].TokenHash.Should().NotBe(session.RefreshToken)
            .And.Be(context.TokenGenerator.Hash(session.RefreshToken));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_fail_the_same_way()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        await context.AddUserAsync();
        var handler = context.LoginHandler();

        var wrongPassword = () => handler.Handle(
            new LoginCommand("admin@toeicspace.vn", "Wrong-password-1"),
            CancellationToken.None);
        var unknownEmail = () => handler.Handle(
            new LoginCommand("nobody@toeicspace.vn", IdentityTestContext.Password),
            CancellationToken.None);

        var first = (await wrongPassword.Should().ThrowAsync<AppException>()).Which;
        var second = (await unknownEmail.Should().ThrowAsync<AppException>()).Which;

        first.Type.Should().Be(ErrorType.Unauthenticated);
        first.Code.Should().Be(ErrorCodes.InvalidCredentials);
        second.Type.Should().Be(first.Type);
        second.Code.Should().Be(first.Code);
        second.Message.Should().Be(first.Message);

        context.LoginAttemptLimiter.FailuresFor("admin@toeicspace.vn").Should().Be(1);
        context.LoginAttemptLimiter.FailuresFor("nobody@toeicspace.vn").Should().Be(1);
    }

    [Fact]
    public async Task Locked_out_email_is_refused_even_with_the_correct_password()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        await context.AddUserAsync();
        var handler = context.LoginHandler();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await FluentActions
                .Awaiting(() => handler.Handle(
                    new LoginCommand("admin@toeicspace.vn", "Wrong-password-1"),
                    CancellationToken.None))
                .Should().ThrowAsync<AppException>();
        }

        var locked = await FluentActions
            .Awaiting(() => handler.Handle(
                new LoginCommand("admin@toeicspace.vn", IdentityTestContext.Password),
                CancellationToken.None))
            .Should().ThrowAsync<AppException>();

        locked.Which.Type.Should().Be(ErrorType.TooManyRequests);
        locked.Which.Code.Should().Be(ErrorCodes.LoginTemporarilyLocked);
    }

    [Fact]
    public async Task Successful_sign_in_clears_the_failure_counter()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        await context.AddUserAsync();
        var handler = context.LoginHandler();

        await FluentActions
            .Awaiting(() => handler.Handle(new LoginCommand("admin@toeicspace.vn", "nope-nope-1"), CancellationToken.None))
            .Should().ThrowAsync<AppException>();

        await handler.Handle(new LoginCommand("admin@toeicspace.vn", IdentityTestContext.Password), CancellationToken.None);

        context.LoginAttemptLimiter.FailuresFor("admin@toeicspace.vn").Should().Be(0);
    }

    [Theory]
    [InlineData(false, UserStatus.Inactive, ErrorCodes.EmailNotVerified)]
    [InlineData(true, UserStatus.Suspended, ErrorCodes.AccountLocked)]
    [InlineData(true, UserStatus.Inactive, ErrorCodes.AccountLocked)]
    public async Task Accounts_that_cannot_sign_in_get_no_session(
        bool emailVerified,
        UserStatus status,
        string expectedCode)
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync(status: status, emailVerified: emailVerified);

        var result = await FluentActions
            .Awaiting(() => context.LoginHandler().Handle(
                new LoginCommand(user.Email, IdentityTestContext.Password),
                CancellationToken.None))
            .Should().ThrowAsync<AppException>();

        result.Which.Type.Should().Be(ErrorType.Forbidden);
        result.Which.Code.Should().Be(expectedCode);
        (await context.RefreshTokenRowsAsync(user.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Soft_deleted_account_looks_like_a_wrong_password()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync();
        var stored = await context.Db.Users.FindAsync(user.Id);
        stored!.DeletedAt = context.Time.Now.UtcDateTime;
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();

        var result = await FluentActions
            .Awaiting(() => context.LoginHandler().Handle(
                new LoginCommand(user.Email, IdentityTestContext.Password),
                CancellationToken.None))
            .Should().ThrowAsync<AppException>();

        result.Which.Code.Should().Be(ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Oldest_sessions_beyond_the_limit_are_revoked()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync();
        var handler = context.LoginHandler();

        for (var login = 0; login < 5; login++)
        {
            await handler.Handle(new LoginCommand(user.Email, IdentityTestContext.Password), CancellationToken.None);
            context.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var rows = await context.RefreshTokenRowsAsync(user.Id);
        rows.Should().HaveCount(5);
        rows.Count(token => !token.IsRevoked).Should().Be(context.SessionOptions.MaxActiveSessions);
        rows.Take(2).Should().OnlyContain(token => token.IsRevoked);
    }
}
