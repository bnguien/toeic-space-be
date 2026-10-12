using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Identity.Application.Features.ResendVerification.Commands;
using ToeicSpace.Identity.Application.Features.VerifyEmail.Commands;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Tests;

public sealed class ResendVerificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithoutPreviousChallenge_CreatesChallengeAndPublishesEvent()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FullName = "Ada Lovelace"
        };
        var repository = new FakeUserRepository();
        repository.Users.Add(user);
        var challengeStore = new FakeOtpChallengeStore();
        var eventPublisher = new FakeIntegrationEventPublisher();
        var handler = CreateHandler(
            repository,
            challengeStore,
            eventPublisher);

        var result = await handler.Handle(
            new ResendVerificationCommand(" ADA@example.com "),
            CancellationToken.None);

        Assert.Equal(user.Id, result.UserId);
        var challenge = Assert.Single(challengeStore.Challenges).Value;
        Assert.Equal(user.Id, challenge.UserId);
        Assert.Equal("hash:123456", challenge.OtpHash);
        var integrationEvent = Assert.IsType<UserRegistrationOtpRequestedEvent>(
            eventPublisher.LastEvent);
        Assert.Equal(user.Email, integrationEvent.Email);
    }

    [Fact]
    public async Task HandleAsync_AfterTwoResends_InvalidatesEveryOlderChallenge()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FullName = "Ada Lovelace",
            Status = UserStatus.Inactive
        };
        var repository = new FakeUserRepository();
        repository.Users.Add(user);
        var challengeStore = new FakeOtpChallengeStore();
        var originalChallengeId = await challengeStore.ReplaceChallengeAsync(
            new OtpChallenge(
                user.Id,
                "hash:111111",
                OtpChallenge.VerifyEmailPurpose,
                0),
            CancellationToken.None);
        var handler = CreateHandler(
            repository,
            challengeStore,
            new FakeIntegrationEventPublisher(),
            new SequenceOtpGenerator("222222", "333333"));

        var firstResend = await handler.Handle(
            new ResendVerificationCommand(user.Email),
            CancellationToken.None);
        challengeStore.ReleaseResendCooldown(user.Id);
        var secondResend = await handler.Handle(
            new ResendVerificationCommand(user.Email),
            CancellationToken.None);

        var verifyHandler = CreateVerifyHandler(repository, challengeStore);
        var originalException = await Assert.ThrowsAsync<AppException>(() =>
            verifyHandler.Handle(
                new VerifyEmailCommand(originalChallengeId, "111111"),
                CancellationToken.None));
        var firstResendException = await Assert.ThrowsAsync<AppException>(() =>
            verifyHandler.Handle(
                new VerifyEmailCommand(firstResend.ChallengeId, "222222"),
                CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, originalException.Code);
        Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, firstResendException.Code);

        var result = await verifyHandler.Handle(
            new VerifyEmailCommand(secondResend.ChallengeId, "333333"),
            CancellationToken.None);

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task HandleAsync_WithConcurrentResends_LeavesOnlyLastReplacementActive()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FullName = "Ada Lovelace",
            Status = UserStatus.Inactive
        };
        var repository = new FakeUserRepository();
        repository.Users.Add(user);
        var challengeStore = new FakeOtpChallengeStore
        {
            EnforceResendCooldown = false
        };
        var handler = CreateHandler(
            repository,
            challengeStore,
            new FakeIntegrationEventPublisher(),
            new ConcurrentOtpGenerator());

        var resendTasks = Enumerable.Range(0, 10)
            .Select(_ => handler.Handle(
                new ResendVerificationCommand(user.Email),
                CancellationToken.None))
            .ToArray();
        var results = await Task.WhenAll(resendTasks);

        var activeChallenge = Assert.Single(challengeStore.Challenges);
        Assert.Equal(
            activeChallenge.Key,
            challengeStore.ActiveChallengeIds[user.Id]);
        Assert.Equal(
            10,
            results.Select(result => result.ChallengeId).Distinct().Count());

        var verifyHandler = CreateVerifyHandler(repository, challengeStore);
        foreach (var staleResult in results.Where(
                     result => result.ChallengeId != activeChallenge.Key))
        {
            var exception = await Assert.ThrowsAsync<AppException>(() =>
                verifyHandler.Handle(
                    new VerifyEmailCommand(staleResult.ChallengeId, "000000"),
                    CancellationToken.None));
            Assert.Equal(ErrorCodes.InvalidOrExpiredOtp, exception.Code);
        }

        var activeOtp = activeChallenge.Value.OtpHash["hash:".Length..];
        var verified = await verifyHandler.Handle(
            new VerifyEmailCommand(activeChallenge.Key, activeOtp),
            CancellationToken.None);

        Assert.Equal(user.Id, verified.UserId);
    }

    [Fact]
    public async Task HandleAsync_DuringCooldown_ReturnsClearRateLimitError()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FullName = "Ada Lovelace"
        };
        var repository = new FakeUserRepository();
        repository.Users.Add(user);
        var handler = CreateHandler(
            repository,
            new FakeOtpChallengeStore(),
            new FakeIntegrationEventPublisher());

        await handler.Handle(
            new ResendVerificationCommand(user.Email),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new ResendVerificationCommand(user.Email),
            CancellationToken.None));

        Assert.Equal(ErrorType.TooManyRequests, exception.Type);
        Assert.Equal(ErrorCodes.OtpRateLimitExceeded, exception.Code);
        Assert.Contains("60 seconds", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithVerifiedUser_ReturnsOpaqueResult()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            EmailVerifiedAt = DateTime.UtcNow
        });
        var challengeStore = new FakeOtpChallengeStore();
        var eventPublisher = new FakeIntegrationEventPublisher();
        var handler = CreateHandler(
            repository,
            challengeStore,
            eventPublisher);

        var result = await handler.Handle(
            new ResendVerificationCommand("ada@example.com"),
            CancellationToken.None);

        Assert.Empty(challengeStore.Challenges);
        Assert.Null(eventPublisher.LastEvent);
        Assert.True(Guid.TryParseExact(result.ChallengeId, "N", out _));
    }

    private static ResendVerificationHandler CreateHandler(
        FakeUserRepository repository,
        FakeOtpChallengeStore challengeStore,
        FakeIntegrationEventPublisher eventPublisher,
        IOtpGenerator? otpGenerator = null)
        => new(
            repository,
            otpGenerator ?? new FakeOtpGenerator(),
            new FakeOtpHasher(),
            challengeStore,
            eventPublisher,
            NullLogger<ResendVerificationHandler>.Instance);

    private static VerifyEmailHandler CreateVerifyHandler(
        FakeUserRepository repository,
        FakeOtpChallengeStore challengeStore)
        => new(
            repository,
            challengeStore,
            new FakeOtpHasher(),
            NullLogger<VerifyEmailHandler>.Instance);
}
