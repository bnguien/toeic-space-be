using ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Identity.Application.Features.Analytics.Results;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.UnitTests.Analytics;

/// <summary>Accounts, sign-ins and links for analytics tests. The clock of <see cref="IdentityTestContext"/> is 2026-09-16 08:00 UTC.</summary>
internal static class AnalyticsSeed
{
    public static readonly DateTime Recent = new(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc);

    public static readonly DateTime LongAgo = new(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);

    public static async Task<User> AddAccountAsync(
        this IdentityTestContext context,
        UserRole role = UserRole.User,
        UserStatus status = UserStatus.Active,
        DateTime? createdAt = null,
        DateTime? deletedAt = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Analytics Person",
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-a-real-hash",
            Role = role,
            Status = status,
            CreatedAt = createdAt ?? Recent,
            DeletedAt = deletedAt
        };

        context.Db.Users.Add(user);
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();

        return user;
    }

    public static async Task<List<User>> AddAccountsAsync(
        this IdentityTestContext context,
        int count,
        UserRole role = UserRole.User,
        DateTime? createdAt = null)
    {
        var users = new List<User>();
        for (var index = 0; index < count; index++)
        {
            users.Add(await context.AddAccountAsync(role, createdAt: createdAt));
        }

        return users;
    }

    /// <summary>A refresh token. A sign-in starts a session; a renewal reuses the session expiry.</summary>
    public static async Task AddRefreshTokenAsync(
        this IdentityTestContext context,
        Guid userId,
        DateTime createdAt,
        DateTime? sessionExpiresAt = null)
    {
        context.Db.UserTokens.Add(new UserToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N"),
            Type = TokenType.RefreshToken,
            ExpiresAt = sessionExpiresAt ?? createdAt.AddDays(7),
            CreatedAt = createdAt
        });

        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();
    }

    public static async Task AddOAuthLinkAsync(this IdentityTestContext context, Guid userId)
    {
        context.Db.UserOAuthAccounts.Add(new UserOAuth
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = OAuthProvider.Google,
            ProviderSubject = Guid.NewGuid().ToString("N"),
            CreatedAt = Recent
        });

        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();
    }

    public static Task<AnalyticsResultDto> QueryAsync(this IdentityTestContext context, QueryAnalyticsQuery query)
        => new QueryAnalyticsHandler(context.Db, context.Time).Handle(query, CancellationToken.None);

    public static object? Cell(this AnalyticsResultDto result, int row, string column)
        => result.Rows[row][column];
}
