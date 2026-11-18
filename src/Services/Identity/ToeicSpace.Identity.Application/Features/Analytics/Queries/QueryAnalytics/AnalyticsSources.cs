using Microsoft.EntityFrameworkCore;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;

/// <summary>Accounts created on one day with one role, counted in the database.</summary>
public sealed record RegistrationPartial(
    UserRole Role,
    DateTime Day,
    int Accounts);

/// <summary>Accounts with one role and status, counted in the database.</summary>
public sealed record AccountPartial(
    UserRole Role,
    UserStatus Status,
    int Accounts);

/// <summary>Distinct accounts linked to one provider, counted in the database.</summary>
public sealed record ProviderPartial(
    OAuthProvider Provider,
    int Accounts);

/// <summary>A sign-in: the first refresh token of a session. The account ID never leaves the handler.</summary>
public sealed record SignInFact(
    Guid UserId,
    UserRole Role,
    DateTime StartedAt);

/// <summary>A day on which an account received a refresh token (sign-in or session renewal).</summary>
public sealed record ActiveDayFact(
    Guid UserId,
    UserRole Role,
    DateTime Day);

/// <summary>
/// The fixed EF Core queries behind the Identity analytics metrics. Request values only become typed parameters
/// (dates and enum values); grouping keys and columns never come from input.
/// </summary>
public static class AnalyticsSources
{
    /// <summary>Accounts created in the range, soft-deleted accounts excluded.</summary>
    public static IQueryable<RegistrationPartial> Registrations(
        IIdentityDbContext context,
        AnalyticsRequest request)
    {
        var from = request.Range!.FromUtc;
        var to = request.Range.ToUtcExclusive;

        var users = context.Users
            .AsNoTracking()
            .Where(user => user.DeletedAt == null && user.CreatedAt >= from && user.CreatedAt < to);

        if (request.Role is { } role)
        {
            users = users.Where(user => user.Role == role);
        }

        return users
            .GroupBy(user => new { user.Role, Day = user.CreatedAt.Date })
            .Select(group => new RegistrationPartial(group.Key.Role, group.Key.Day, group.Count()));
    }

    /// <summary>
    /// Accounts existing at the end of <c>to</c> when the request has a snapshot date, otherwise the accounts
    /// that are not soft-deleted now.
    /// </summary>
    public static IQueryable<AccountPartial> Accounts(
        IIdentityDbContext context,
        AnalyticsRequest request)
        => CountByRoleAndStatus(Filter(ExistingUsers(context, request), request));

    /// <summary>Accounts existing at <c>to</c>, created at least the inactivity window earlier, without a refresh token inside it.</summary>
    public static IQueryable<AccountPartial> InactiveAccounts(
        IIdentityDbContext context,
        AnalyticsRequest request,
        int inactivityDays)
    {
        var to = request.Range!.ToUtcExclusive;
        var cutoff = to.AddDays(-inactivityDays);

        var users = Filter(ExistingUsers(context, request), request)
            .Where(user => user.CreatedAt < cutoff)
            .Where(user => !user.Tokens.Any(token =>
                token.Type == TokenType.RefreshToken
                && token.CreatedAt >= cutoff
                && token.CreatedAt < to));

        return CountByRoleAndStatus(users);
    }

    /// <summary>Distinct accounts (not soft-deleted) linked to each provider.</summary>
    public static IQueryable<ProviderPartial> OAuthLinks(
        IIdentityDbContext context,
        AnalyticsRequest request)
    {
        var links = context.UserOAuthAccounts
            .AsNoTracking()
            .Where(link => link.User.DeletedAt == null);

        if (request.Provider is { } provider)
        {
            links = links.Where(link => link.Provider == provider);
        }

        // Two-level grouping (account, then provider) counts each account once per provider.
        return links
            .GroupBy(link => new { link.Provider, link.UserId })
            .Select(group => new { group.Key.Provider, group.Key.UserId })
            .GroupBy(link => link.Provider)
            .Select(group => new ProviderPartial(group.Key, group.Count()));
    }

    /// <summary>
    /// Sessions started in the range. Every refresh token of a session shares the session expiry (rotation keeps
    /// it), so the earliest token of each (account, expiry) pair is the sign-in.
    /// </summary>
    public static IQueryable<SignInFact> SignIns(
        IIdentityDbContext context,
        AnalyticsRequest request)
    {
        var from = request.Range!.FromUtc;
        var to = request.Range.ToUtcExclusive;

        var tokens = RefreshTokens(context, request)
            .Where(token => token.ExpiresAt > from && token.CreatedAt < to);

        return tokens
            .Select(token => new { token.UserId, token.User.Role, token.ExpiresAt, token.CreatedAt })
            .GroupBy(token => new { token.UserId, token.Role, token.ExpiresAt })
            .Select(session => new
            {
                session.Key.UserId,
                session.Key.Role,
                StartedAt = session.Min(token => token.CreatedAt)
            })
            .Where(session => session.StartedAt >= from)
            .Select(session => new SignInFact(session.UserId, session.Role, session.StartedAt));
    }

    /// <summary>(account, day) pairs with at least one refresh token issued in the range.</summary>
    public static IQueryable<ActiveDayFact> ActiveDays(
        IIdentityDbContext context,
        AnalyticsRequest request)
    {
        var from = request.Range!.FromUtc;
        var to = request.Range.ToUtcExclusive;

        return RefreshTokens(context, request)
            .Where(token => token.CreatedAt >= from && token.CreatedAt < to)
            .Select(token => new { token.UserId, token.User.Role, Day = token.CreatedAt.Date })
            .Distinct()
            .Select(row => new ActiveDayFact(row.UserId, row.Role, row.Day));
    }

    private static IQueryable<UserToken> RefreshTokens(
        IIdentityDbContext context,
        AnalyticsRequest request)
    {
        var tokens = context.UserTokens
            .AsNoTracking()
            .Where(token => token.Type == TokenType.RefreshToken);

        if (request.Role is { } role)
        {
            tokens = tokens.Where(token => token.User.Role == role);
        }

        return tokens;
    }

    private static IQueryable<User> ExistingUsers(
        IIdentityDbContext context,
        AnalyticsRequest request)
    {
        var users = context.Users.AsNoTracking();

        if (request.Range is null)
        {
            return users.Where(user => user.DeletedAt == null);
        }

        var to = request.Range.ToUtcExclusive;

        return users.Where(user => user.CreatedAt < to && (user.DeletedAt == null || user.DeletedAt >= to));
    }

    private static IQueryable<User> Filter(IQueryable<User> users, AnalyticsRequest request)
    {
        if (request.Role is { } role)
        {
            users = users.Where(user => user.Role == role);
        }

        if (request.Status is { } status)
        {
            users = users.Where(user => user.Status == status);
        }

        return users;
    }

    private static IQueryable<AccountPartial> CountByRoleAndStatus(IQueryable<User> users)
        => users
            .GroupBy(user => new { user.Role, user.Status })
            .Select(group => new AccountPartial(group.Key.Role, group.Key.Status, group.Count()));
}
