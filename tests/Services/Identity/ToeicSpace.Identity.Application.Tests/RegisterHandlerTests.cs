using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Tests;

public sealed class RegisterHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidRequest_PersistsNormalizedUserAndChallenge()
    {
        var repository = new FakeUserRepository();
        var challengeStore = new FakeOtpChallengeStore();
        var eventPublisher = new FakeIntegrationEventPublisher();
        var handler = CreateHandler(repository, challengeStore, eventPublisher);

        var response = await handler.Handle(
            new RegisterCommand(
                "  Ada Lovelace  ",
                "  Ada@Example.COM ",
                "+84 (90) 123-4567",
                "correct-horse",
                "correct-horse"),
            CancellationToken.None);

        var user = Assert.Single(repository.Users);
        Assert.Equal(response.UserId, user.Id);
        Assert.Equal("Ada Lovelace", user.FullName);
        Assert.Equal("ada@example.com", user.Email);
        Assert.Equal("+84901234567", user.Phone);
        Assert.Equal("hashed:correct-horse", user.PasswordHash);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Equal(UserStatus.Inactive, user.Status);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);

        var challenge = Assert.Single(challengeStore.Challenges).Value;
        Assert.Equal(user.Id, challenge.UserId);
        Assert.Equal("hash:123456", challenge.OtpHash);
        Assert.NotEqual("123456", challenge.OtpHash);

        var integrationEvent = Assert.IsType<UserRegistrationOtpRequestedEvent>(
            eventPublisher.LastEvent);
        Assert.Equal(user.Email, integrationEvent.Email);
        Assert.Equal(user.FullName, integrationEvent.FullName);
        Assert.Equal("123456", integrationEvent.Otp);
    }

    [Fact]
    public async Task HandleAsync_WithMismatchedPasswords_ReturnsSpecificErrorCode()
    {
        var repository = new FakeUserRepository();
        var handler = CreateHandler(
            repository,
            new FakeOtpChallengeStore(),
            new FakeIntegrationEventPublisher());

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new RegisterCommand(
                "Ada Lovelace",
                "ada@example.com",
                null,
                "correct-horse",
                "different-password"),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.PasswordsDoNotMatch, exception.Code);
        Assert.Empty(repository.Users);
    }

    [Fact]
    public async Task HandleAsync_WithVietnameseLocalPhone_NormalizesToInternationalFormat()
    {
        var repository = new FakeUserRepository();
        var handler = CreateHandler(
            repository,
            new FakeOtpChallengeStore(),
            new FakeIntegrationEventPublisher());

        await handler.Handle(
            new RegisterCommand(
                "Ada Lovelace",
                "ada@example.com",
                "03092663097",
                "correct-horse",
                "correct-horse"),
            CancellationToken.None);

        var user = Assert.Single(repository.Users);
        Assert.Equal("+843092663097", user.Phone);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateEmail_ReturnsGenericConflict()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(new User { Email = "ada@example.com" });
        var handler = CreateHandler(
            repository,
            new FakeOtpChallengeStore(),
            new FakeIntegrationEventPublisher());

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new RegisterCommand(
                "Ada Lovelace",
                " ADA@example.com ",
                null,
                "correct-horse",
                "correct-horse"),
            CancellationToken.None));

        Assert.Single(repository.Users);
        Assert.Equal(ErrorType.Conflict, exception.Type);
        Assert.Equal(ErrorCodes.RegistrationCouldNotBeCompleted, exception.Code);
        Assert.Equal(
            "Unable to complete registration with the provided information.",
            exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateNormalizedPhone_ReturnsGenericConflict()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(new User
        {
            Email = "existing@example.com",
            Phone = "+84901234567"
        });
        var handler = CreateHandler(
            repository,
            new FakeOtpChallengeStore(),
            new FakeIntegrationEventPublisher());

        var exception = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new RegisterCommand(
                "Ada Lovelace",
                "ada@example.com",
                "+84 (90) 123-4567",
                "correct-horse",
                "correct-horse"),
            CancellationToken.None));

        Assert.Single(repository.Users);
        Assert.Equal(ErrorType.Conflict, exception.Type);
        Assert.Equal(ErrorCodes.RegistrationCouldNotBeCompleted, exception.Code);
    }

    private static RegisterHandler CreateHandler(
        FakeUserRepository repository,
        FakeOtpChallengeStore challengeStore,
        FakeIntegrationEventPublisher eventPublisher)
        => new(
            repository,
            new FakePasswordHasher(),
            new FakeOtpGenerator(),
            new FakeOtpHasher(),
            challengeStore,
            eventPublisher,
            NullLogger<RegisterHandler>.Instance);
}
