using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Identity.Application.Features.CleanupInactiveAccounts.Commands;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Tests;

public sealed class CleanupInactiveAccountsHandlerTests
{
    private static readonly DateTimeOffset Now = new(
        2026,
        9,
        16,
        0,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WithExpiredInactiveUser_HardDeletesUser()
    {
        var expiredUser = CreateUser(
            UserStatus.Inactive,
            Now.UtcDateTime.AddDays(-4));
        var repository = new FakeUserRepository();
        repository.Users.Add(expiredUser);
        var handler = CreateHandler(repository);

        var deletedCount = await handler.Handle(
            new CleanupInactiveAccountsCommand(),
            CancellationToken.None);

        Assert.Equal(1, deletedCount);
        Assert.DoesNotContain(expiredUser, repository.Users);
        Assert.Equal(Now.UtcDateTime.AddDays(-3), repository.LastInactiveOlderThanCutoff);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WithRecentInactiveUser_PreservesUser()
    {
        var recentUser = CreateUser(
            UserStatus.Inactive,
            Now.UtcDateTime.AddDays(-2));
        var repository = new FakeUserRepository();
        repository.Users.Add(recentUser);
        var handler = CreateHandler(repository);

        var deletedCount = await handler.Handle(
            new CleanupInactiveAccountsCommand(),
            CancellationToken.None);

        Assert.Equal(0, deletedCount);
        Assert.Contains(recentUser, repository.Users);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WithOldActiveUser_PreservesUser()
    {
        var activeUser = CreateUser(
            UserStatus.Active,
            Now.UtcDateTime.AddDays(-30));
        var repository = new FakeUserRepository();
        repository.Users.Add(activeUser);
        var handler = CreateHandler(repository);

        var deletedCount = await handler.Handle(
            new CleanupInactiveAccountsCommand(),
            CancellationToken.None);

        Assert.Equal(0, deletedCount);
        Assert.Contains(activeUser, repository.Users);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    private static User CreateUser(UserStatus status, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            Status = status,
            CreatedAt = createdAt
        };

    private static CleanupInactiveAccountsHandler CreateHandler(
        FakeUserRepository repository)
        => new(
            repository,
            new InactiveAccountCleanupOptions
            {
                InactiveAccountRetentionDays = 3
            },
            new FixedTimeProvider(Now),
            NullLogger<CleanupInactiveAccountsHandler>.Instance);
}
