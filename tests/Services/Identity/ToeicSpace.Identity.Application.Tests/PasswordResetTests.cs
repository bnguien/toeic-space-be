using MediatR;
using ToeicSpace.Identity.Application.Common.Behaviors;
using ToeicSpace.Identity.Application.Features.ChangePassword.Commands;
using ToeicSpace.Identity.Application.Features.PasswordReset.Commands;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Tests;

public sealed class PasswordResetTests
{
    private const string NewPassword = "NewPassword1!";

    [Theory]
    [InlineData(-1, "The password reset token is required.")]
    [InlineData(0, "The password reset token is required.")]
    [InlineData(129, "The password reset token must not exceed 128 characters.")]
    public async Task InvalidToken_IsRejectedByPipelineBeforeHandler(int length, string message)
    {
        var token = length < 0 ? null! : new string('t', length);
        var command = new ConfirmPasswordResetCommand(token, NewPassword, NewPassword);
        var behavior = new ValidationBehavior<ConfirmPasswordResetCommand, Unit>(
            [new ConfirmPasswordResetValidator()]);
        var handlerCalled = false;

        var exception = await Assert.ThrowsAsync<AppException>(() => behavior.Handle(
            command,
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None));

        Assert.False(handlerCalled);
        Assert.Equal(ErrorType.Validation, exception.Type);
        Assert.Equal(ErrorCodes.TokenInvalid, exception.Code);
        Assert.Equal(message, exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public async Task ValidToken_PassesValidationAndCallsHandler(int length)
    {
        var command = new ConfirmPasswordResetCommand(new string('t', length), NewPassword, NewPassword);
        var behavior = new ValidationBehavior<ConfirmPasswordResetCommand, Unit>(
            [new ConfirmPasswordResetValidator()]);
        var handlerCalled = false;

        await behavior.Handle(command, _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Unit.Value);
        }, CancellationToken.None);

        Assert.True(handlerCalled);
    }

    [Fact]
    public async Task PasswordFieldErrors_KeepExistingValidationResponse()
    {
        var command = new ConfirmPasswordResetCommand("token", "weak", "different");
        var behavior = new ValidationBehavior<ConfirmPasswordResetCommand, Unit>(
            [new ConfirmPasswordResetValidator()]);
        var handlerCalled = false;

        var exception = await Assert.ThrowsAsync<AppException>(() => behavior.Handle(
            command,
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None));

        Assert.False(handlerCalled);
        Assert.Equal(ErrorCodes.ValidationError, exception.Code);
        Assert.NotNull(exception.ValidationErrors);
        Assert.Contains(nameof(command.NewPassword), exception.ValidationErrors.Keys);
        Assert.Contains(nameof(command.ConfirmPassword), exception.ValidationErrors.Keys);
    }

    [Theory]
    [InlineData("missing-grant", "The password reset session was not found or has expired.", 0, 0)]
    [InlineData("ineligible-account", "The account associated with the password reset session is no longer eligible for a password reset.", 0, 0)]
    [InlineData("changed-password", "The account password has changed since the password reset session was issued.", 0, 0)]
    [InlineData("consumed-grant", "The password reset session could not be consumed because it has expired, was already used, or was replaced.", 1, 0)]
    [InlineData("concurrent-change", "The password could not be reset because the account or its password changed during the request.", 1, 1)]
    public async Task InvalidSession_PreservesErrorCodeAndSideEffects(
        string scenario, string message, int consumeCalls, int changeCalls)
    {
        var user = CreateUser();
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var store = new ResetStore { Grant = new(user.Id, "token-hash:" + user.PasswordHash) };
        var passwords = new PasswordRepository();

        switch (scenario)
        {
            case "missing-grant": store.Grant = null; break;
            case "ineligible-account": user.Status = UserStatus.Inactive; break;
            case "changed-password": user.PasswordHash = "hashed:ChangedPassword1!"; break;
            case "consumed-grant": store.CanConsume = false; break;
            case "concurrent-change": passwords.CanChange = false; break;
        }

        var handler = new ConfirmPasswordResetHandler(store, users, passwords,
            new FakePasswordHasher(), new TokenGenerator());
        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new("token", NewPassword, NewPassword), CancellationToken.None));

        Assert.Equal(ErrorType.Validation, exception.Type);
        Assert.Equal(ErrorCodes.TokenInvalid, exception.Code);
        Assert.Equal(message, exception.Message);
        Assert.Equal(consumeCalls, store.ConsumeCalls);
        Assert.Equal(changeCalls, passwords.ChangeCalls);
    }

    [Fact]
    public async Task SuccessfulReset_ConsumesGrantBeforeChangingPassword_AndRejectsReplay()
    {
        var user = CreateUser();
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var store = new ResetStore { Grant = new(user.Id, "token-hash:" + user.PasswordHash) };
        var passwords = new PasswordRepository { BeforeChange = () => Assert.Equal(1, store.ConsumeCalls) };
        var handler = new ConfirmPasswordResetHandler(store, users, passwords,
            new FakePasswordHasher(), new TokenGenerator());
        var command = new ConfirmPasswordResetCommand("token", NewPassword, NewPassword);

        await handler.Handle(command, CancellationToken.None);
        var replay = await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Equal(ErrorCodes.TokenInvalid, replay.Code);
        Assert.Equal(1, store.ConsumeCalls);
        Assert.Equal(1, passwords.ChangeCalls);
        Assert.Equal((user.Id, user.PasswordHash!, "hashed:" + NewPassword), passwords.LastChange);
    }

    [Fact]
    public async Task SamePassword_DoesNotConsumeGrantOrChangePassword()
    {
        var user = CreateUser();
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var store = new ResetStore { Grant = new(user.Id, "token-hash:" + user.PasswordHash) };
        var passwords = new PasswordRepository();
        var handler = new ConfirmPasswordResetHandler(store, users, passwords,
            new FakePasswordHasher(), new TokenGenerator());

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new("token", "OldPassword1!", "OldPassword1!"), CancellationToken.None));

        Assert.Equal(ErrorCodes.PasswordSameAsOld, exception.Code);
        Assert.Equal(0, store.ConsumeCalls);
        Assert.Equal(0, passwords.ChangeCalls);
    }

    [Theory]
    [InlineData("invalid-otp", "The password reset OTP is incorrect, has expired, was already used, or has exceeded the maximum failed attempts.", 0)]
    [InlineData("missing-grant", "The verified password reset session was not found or has expired.", 1)]
    [InlineData("ineligible-account", "The account associated with the verified OTP is no longer eligible for a password reset.", 1)]
    [InlineData("changed-password", "The account password has changed since the password reset OTP was issued.", 1)]
    public async Task RejectedVerification_PreservesErrorCodeAndGrantCleanup(
        string scenario, string message, int consumeCalls)
    {
        var user = CreateUser();
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var store = new ResetStore { Grant = new(user.Id, "token-hash:" + user.PasswordHash) };
        switch (scenario)
        {
            case "invalid-otp": store.CanVerify = false; break;
            case "missing-grant": store.Grant = null; break;
            case "ineligible-account": user.Status = UserStatus.Inactive; break;
            case "changed-password": user.PasswordHash = "hashed:ChangedPassword1!"; break;
        }

        var handler = new VerifyPasswordResetHandler(store, users, new FakeOtpHasher(),
            new TokenGenerator(), new PasswordResetOptions(), new FixedTimeProvider(DateTimeOffset.UtcNow));
        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new("user@example.com", "123456"), CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, exception.Code);
        Assert.Equal(message, exception.Message);
        Assert.Equal(consumeCalls, store.ConsumeCalls);
    }

    [Theory]
    [InlineData("wrong-password", "The current password is incorrect.", 0)]
    [InlineData("ineligible-account", "The account was not found or is no longer eligible for a password change.", 0)]
    [InlineData("concurrent-change", "The password could not be changed because the account or its password changed during the request.", 1)]
    public async Task RejectedPasswordChange_PreservesErrorCodeAndSideEffects(
        string scenario, string message, int changeCalls)
    {
        var user = CreateUser();
        var users = new FakeUserRepository();
        users.Users.Add(user);
        if (scenario == "ineligible-account") user.Status = UserStatus.Inactive;
        var passwords = new PasswordRepository { CanChange = scenario != "concurrent-change" };
        var store = new ResetStore { Grant = new(user.Id, "token-hash:" + user.PasswordHash) };
        var handler = new ChangePasswordHandler(users, passwords, new FakePasswordHasher(), store,
            new FakeOtpHasher(), new TokenGenerator());
        var currentPassword = scenario == "wrong-password" ? "wrong" : "OldPassword1!";

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new(user.Id, currentPassword, NewPassword, NewPassword, "123456"), CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidCredentials, exception.Code);
        Assert.Equal(message, exception.Message);
        Assert.Equal(changeCalls, passwords.ChangeCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public async Task PasswordChange_RequiresSixDigitOtpBeforeHandler(string otp)
    {
        var behavior = new ValidationBehavior<ChangePasswordCommand, Unit>([new ChangePasswordValidator()]);
        var handlerCalled = false;
        await Assert.ThrowsAsync<AppException>(() => behavior.Handle(
            new(Guid.NewGuid(), "OldPassword1!", NewPassword, NewPassword, otp), _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Unit.Value);
            }, CancellationToken.None));
        Assert.False(handlerCalled);
    }

    [Theory]
    [InlineData("wrong-otp")]
    [InlineData("expired-otp")]
    [InlineData("recovery-otp")]
    [InlineData("other-user")]
    [InlineData("changed-password")]
    [InlineData("missing-grant")]
    [InlineData("consumed-grant")]
    public async Task PasswordChange_InvalidOtpCannotUpdatePassword(string scenario)
    {
        var user = CreateUser();
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var store = new ResetStore
        {
            Grant = new(user.Id, "token-hash:" + user.PasswordHash),
            ExpectedScope = user.Id.ToString("N"),
            ExpectedOtpHash = "hash:123456"
        };
        switch (scenario)
        {
            case "expired-otp": store.CanVerify = false; break;
            case "recovery-otp": store.ExpectedScope = user.Email; break;
            case "other-user": store.Grant = new(Guid.NewGuid(), "token-hash:" + user.PasswordHash); break;
            case "changed-password": store.Grant = new(user.Id, "old-stamp"); break;
            case "missing-grant": store.Grant = null; break;
            case "consumed-grant": store.CanConsume = false; break;
        }
        var passwords = new PasswordRepository();
        var handler = new ChangePasswordHandler(users, passwords, new FakePasswordHasher(), store,
            new FakeOtpHasher(), new TokenGenerator());
        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new(user.Id, "OldPassword1!", NewPassword, NewPassword,
                scenario == "wrong-otp" ? "654321" : "123456"), CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, exception.Code);
        Assert.Equal(0, passwords.ChangeCalls);
    }

    [Fact]
    public async Task PasswordChange_ConsumesOtpBeforeUpdatingPassword_AndRejectsReplay()
    {
        var user = CreateUser();
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var store = new ResetStore { Grant = new(user.Id, "token-hash:" + user.PasswordHash) };
        var passwords = new PasswordRepository { BeforeChange = () => Assert.Equal(1, store.ConsumeCalls) };
        var handler = new ChangePasswordHandler(users, passwords, new FakePasswordHasher(), store,
            new FakeOtpHasher(), new TokenGenerator());
        var command = new ChangePasswordCommand(user.Id, "OldPassword1!", NewPassword, NewPassword, "123456");

        await handler.Handle(command, CancellationToken.None);
        var replay = await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, replay.Code);
        Assert.Equal(user.Id.ToString("N"), store.LastScope);
        Assert.Equal(1, passwords.ChangeCalls);
        Assert.Equal((user.Id, user.PasswordHash!, "hashed:" + NewPassword), passwords.LastChange);
    }

    [Theory]
    [InlineData("eligible")]
    [InlineData("cooldown")]
    [InlineData("wrong-password")]
    [InlineData("unverified")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    public async Task ChangeOtpRequest_UsesAuthenticatedAccountAndRejectsIneligibleRequests(string scenario)
    {
        var user = CreateUser();
        if (scenario == "unverified") user.EmailVerifiedAt = null;
        if (scenario == "inactive") user.Status = UserStatus.Inactive;
        if (scenario == "deleted") user.DeletedAt = DateTime.UtcNow;
        var users = new FakeUserRepository();
        users.Users.Add(user);
        var store = new ResetStore { RequestId = scenario == "cooldown" ? null : "request-id" };
        var publisher = new FakeIntegrationEventPublisher();
        var handler = new RequestChangePasswordOtpHandler(users, new FakePasswordHasher(), store, publisher);
        var command = new RequestChangePasswordOtpCommand(user.Id,
            scenario == "wrong-password" ? "wrong" : "OldPassword1!");

        if (scenario is "wrong-password" or "unverified" or "inactive" or "deleted")
        {
            var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Equal(ErrorCodes.InvalidCredentials, exception.Code);
            Assert.Equal(0, store.BeginCalls);
            Assert.Null(publisher.LastEvent);
            return;
        }

        await handler.Handle(command, CancellationToken.None);
        Assert.Equal(user.Id.ToString("N"), store.LastScope);
        if (scenario == "cooldown")
        {
            Assert.Null(publisher.LastEvent);
        }
        else
        {
            Assert.Equal(new PasswordChangeRequestedEvent(user.Id, "request-id"), publisher.LastEvent);
        }
    }

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@example.com",
        PasswordHash = "hashed:OldPassword1!",
        Status = UserStatus.Active,
        EmailVerifiedAt = DateTime.UtcNow
    };

    private sealed class TokenGenerator : IRefreshTokenGenerator
    {
        public string Generate() => "token";
        public string Hash(string token) => "token-hash:" + token;
    }

    private sealed class PasswordRepository : IPasswordRepository
    {
        public bool CanChange { get; set; } = true;
        public int ChangeCalls { get; private set; }
        public Action? BeforeChange { get; init; }
        public (Guid, string, string)? LastChange { get; private set; }

        public Task<bool> TryChangeAsync(Guid userId, string expectedHash, string newHash,
            CancellationToken cancellationToken)
        {
            BeforeChange?.Invoke();
            ChangeCalls++;
            LastChange = (userId, expectedHash, newHash);
            return Task.FromResult(CanChange);
        }
    }

    private sealed class ResetStore : IPasswordResetStore
    {
        public PasswordResetGrant? Grant { get; set; }
        public bool CanConsume { get; set; } = true;
        public bool CanVerify { get; set; } = true;
        public int ConsumeCalls { get; private set; }
        public int BeginCalls { get; private set; }
        public string? RequestId { get; set; } = "request-id";
        public string? ExpectedScope { get; set; }
        public string? ExpectedOtpHash { get; set; }
        public string? LastScope { get; private set; }

        public Task<PasswordResetGrant?> GetGrantAsync(string tokenHash, CancellationToken cancellationToken)
            => Task.FromResult(Grant);

        public Task<bool> ConsumeGrantAsync(string tokenHash, CancellationToken cancellationToken)
        {
            ConsumeCalls++;
            if (!CanConsume) return Task.FromResult(false);
            Grant = null;
            return Task.FromResult(true);
        }

        public Task<string?> BeginRequestAsync(string email, CancellationToken cancellationToken)
        {
            BeginCalls++;
            LastScope = email;
            return Task.FromResult(RequestId);
        }

        public Task<bool> SetOtpAsync(string email, string requestId, Guid userId, string credentialStamp,
            string otpHash, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> VerifyAsync(string email, string otpHash, string tokenHash,
            CancellationToken cancellationToken)
        {
            LastScope = email;
            var verified = CanVerify && (ExpectedScope is null || ExpectedScope == email)
                && (ExpectedOtpHash is null || ExpectedOtpHash == otpHash);
            if (verified) CanVerify = false;
            return Task.FromResult(verified);
        }
    }
}
