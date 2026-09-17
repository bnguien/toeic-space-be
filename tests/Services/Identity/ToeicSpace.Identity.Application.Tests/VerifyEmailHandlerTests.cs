using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Identity.Application.Features.VerifyEmail.Commands;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Tests;

public sealed class VerifyEmailHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithMissingChallenge_ReturnsInvalidOrExpired()
    {
        var handler = CreateHandler(
            new FakeUserRepository(),
            new FakeOtpChallengeStore());

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new VerifyEmailCommand(Guid.NewGuid().ToString("N"), "123456"),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, exception.Code);
    }

    [Fact]
    public async Task HandleAsync_OnFifthWrongAttempt_DeletesChallenge()
    {
        var challengeId = Guid.NewGuid().ToString("N");
        var store = new FakeOtpChallengeStore();
        store.Challenges[challengeId] = new OtpChallenge(
            Guid.NewGuid(),
            "hash:123456",
            OtpChallenge.VerifyEmailPurpose,
            4);
        var handler = CreateHandler(new FakeUserRepository(), store);

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new VerifyEmailCommand(challengeId, "654321"),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, exception.Code);
        Assert.DoesNotContain(challengeId, store.Challenges.Keys);
        Assert.Contains(challengeId, store.DeletedChallengeIds);
    }

    [Fact]
    public async Task HandleAsync_WithCorrectOtp_ActivatesUserAndDeletesChallenge()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Status = UserStatus.Inactive
        };
        var repository = new FakeUserRepository();
        repository.Users.Add(user);
        var challengeId = Guid.NewGuid().ToString("N");
        var store = new FakeOtpChallengeStore();
        store.Challenges[challengeId] = new OtpChallenge(
            user.Id,
            "hash:123456",
            OtpChallenge.VerifyEmailPurpose,
            0);
        var handler = CreateHandler(repository, store);

        var response = await handler.Handle(
            new VerifyEmailCommand(challengeId, "123456"),
            CancellationToken.None);

        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(response.EmailVerifiedAt, user.EmailVerifiedAt);
        Assert.Equal(DateTimeKind.Utc, response.EmailVerifiedAt.Kind);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.DoesNotContain(challengeId, store.Challenges.Keys);
    }

    [Fact]
    public async Task HandleAsync_WithConsumedChallenge_ReturnsInvalidOrExpiredOtp()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com"
        };
        var repository = new FakeUserRepository();
        repository.Users.Add(user);
        var challengeId = Guid.NewGuid().ToString("N");
        var store = new FakeOtpChallengeStore();
        store.Challenges[challengeId] = new OtpChallenge(
            user.Id,
            "hash:123456",
            OtpChallenge.VerifyEmailPurpose,
            0);
        var handler = CreateHandler(repository, store);

        await handler.Handle(
            new VerifyEmailCommand(challengeId, "123456"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new VerifyEmailCommand(challengeId, "123456"),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, exception.Code);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WithCorrectOtpAndMissingUser_ReturnsUserNotFound()
    {
        var challengeId = Guid.NewGuid().ToString("N");
        var store = new FakeOtpChallengeStore();
        store.Challenges[challengeId] = new OtpChallenge(
            Guid.NewGuid(),
            "hash:123456",
            OtpChallenge.VerifyEmailPurpose,
            0);
        var handler = CreateHandler(new FakeUserRepository(), store);

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new VerifyEmailCommand(challengeId, "123456"),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.UserNotFound, exception.Code);
        Assert.Contains(challengeId, store.DeletedChallengeIds);
    }

    private static VerifyEmailHandler CreateHandler(
        FakeUserRepository repository,
        FakeOtpChallengeStore challengeStore)
        => new(
            repository,
            challengeStore,
            new FakeOtpHasher(),
            NullLogger<VerifyEmailHandler>.Instance);
}
