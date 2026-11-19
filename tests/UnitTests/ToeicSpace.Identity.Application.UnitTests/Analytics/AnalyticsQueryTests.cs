using System.Text.Json;
using ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.UnitTests.Analytics;

/// <summary>Whitelisted account metrics over SQLite: counts, snapshots, sign-in sessions and suppression.</summary>
public sealed class AnalyticsQueryTests
{
    [Fact]
    public async Task NewRegistrations_ByDayAndRole_SkipDeletedAccountsAndOtherDates()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        await context.AddAccountsAsync(3);
        await context.AddAccountAsync(UserRole.Teacher);
        await context.AddAccountAsync(deletedAt: AnalyticsSeed.Recent.AddDays(1));
        await context.AddAccountAsync(createdAt: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await context.QueryAsync(new QueryAnalyticsQuery("new_registrations", By: "day,role"));

        result.By.Should().Equal("day", "role");
        result.Rows.Should().HaveCount(2);
        result.Cell(0, "day").Should().Be("2026-09-10");
        result.Cell(0, "role").Should().Be("User");
        result.Cell(0, "value").Should().Be(3.0);
        result.Cell(1, "role").Should().Be("Teacher");
        result.Cell(1, "value").Should().Be(1.0);
        result.Suppressed.Should().Be(0);
        result.From.Should().Be(new DateOnly(2026, 6, 19));
        result.To.Should().Be(new DateOnly(2026, 9, 16));

        var students = await context.QueryAsync(new QueryAnalyticsQuery("new_registrations", Role: "student"));
        students.Rows.Should().ContainSingle();
        students.Cell(0, "value").Should().Be(3.0);
    }

    [Fact]
    public async Task TotalAccounts_CountsAccountsExistingAtTheSnapshotDate()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var september = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await context.AddAccountAsync(createdAt: september);
        await context.AddAccountAsync(createdAt: new DateTime(2026, 9, 10, 23, 59, 0, DateTimeKind.Utc));
        await context.AddAccountAsync(createdAt: new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc));
        await context.AddAccountAsync(createdAt: september, deletedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        await context.AddAccountAsync(createdAt: september, deletedAt: new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc));

        var result = await context.QueryAsync(new QueryAnalyticsQuery("total_accounts", To: "2026-09-10"));

        result.Cell(0, "value").Should().Be(3.0);
        result.From.Should().BeNull();
        result.To.Should().Be(new DateOnly(2026, 9, 10));

        var now = await context.QueryAsync(new QueryAnalyticsQuery("total_accounts"));
        now.Cell(0, "value").Should().Be(3.0);
    }

    [Fact]
    public async Task Mixes_AlwaysGroupByTheirDimension()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        await context.AddAccountsAsync(2);
        await context.AddAccountAsync(UserRole.Teacher);
        await context.AddAccountAsync(UserRole.Teacher, UserStatus.Suspended);
        await context.AddAccountAsync(UserRole.Admin);
        await context.AddAccountAsync(deletedAt: AnalyticsSeed.Recent);

        var roles = await context.QueryAsync(new QueryAnalyticsQuery("account_role_mix"));

        roles.By.Should().Equal("role");
        roles.Rows.Select(row => row["role"]).Should().Equal("User", "Admin", "Teacher");
        roles.Rows.Select(row => row["value"]).Should().Equal(2.0, 1.0, 2.0);

        var activeRoles = await context.QueryAsync(new QueryAnalyticsQuery("account_role_mix", Status: "active"));
        activeRoles.Rows.Select(row => row["value"]).Should().Equal(2.0, 1.0, 1.0);

        var statuses = await context.QueryAsync(new QueryAnalyticsQuery("account_status_mix", Role: "teacher"));
        statuses.Rows.Select(row => row["status"]).Should().Equal("Active", "Suspended");

        var teachers = await context.QueryAsync(new QueryAnalyticsQuery("teacher_count"));
        teachers.Rows.Should().ContainSingle();
        teachers.Cell(0, "value").Should().Be(2.0);

        var students = await context.QueryAsync(new QueryAnalyticsQuery("student_count", By: "status"));
        students.Cell(0, "status").Should().Be("Active");
        students.Cell(0, "value").Should().Be(2.0);
    }

    [Fact]
    public async Task OAuthAccounts_CountEachLiveAccountOncePerProvider()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var linked = await context.AddAccountAsync();
        var other = await context.AddAccountAsync();
        var deleted = await context.AddAccountAsync(deletedAt: AnalyticsSeed.Recent);
        await context.AddOAuthLinkAsync(linked.Id);
        await context.AddOAuthLinkAsync(linked.Id);
        await context.AddOAuthLinkAsync(other.Id);
        await context.AddOAuthLinkAsync(deleted.Id);

        var result = await context.QueryAsync(new QueryAnalyticsQuery("oauth_accounts"));

        result.By.Should().Equal("provider");
        result.Rows.Should().ContainSingle();
        result.Cell(0, "provider").Should().Be("Google");
        result.Cell(0, "value").Should().Be(2.0);
    }

    [Fact]
    public async Task Logins_CountSessionsNotRenewals_AndSuppressSmallGroups()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var signIn = AnalyticsSeed.Recent;
        var sessionEnd = signIn.AddDays(7);

        foreach (var learner in await context.AddAccountsAsync(5))
        {
            await context.AddRefreshTokenAsync(learner.Id, signIn, sessionEnd);
            await context.AddRefreshTokenAsync(learner.Id, signIn.AddDays(1), sessionEnd);
        }

        var teacher = await context.AddAccountAsync(UserRole.Teacher);
        await context.AddRefreshTokenAsync(teacher.Id, signIn);

        var total = await context.QueryAsync(new QueryAnalyticsQuery("logins"));
        total.Cell(0, "value").Should().Be(6.0);
        total.Cell(0, "n").Should().Be(6);

        var byRole = await context.QueryAsync(new QueryAnalyticsQuery("logins", By: "role"));
        byRole.Rows.Select(row => row["role"]).Should().Equal("User", "Teacher");
        byRole.Cell(0, "value").Should().Be(5.0);
        byRole.Cell(1, "value").Should().BeNull();
        byRole.Cell(1, "n").Should().Be(0);
        byRole.Suppressed.Should().Be(1);

        // The renewals on 2026-09-11 belong to sessions started the day before.
        var afterSignIn = await context.QueryAsync(new QueryAnalyticsQuery("logins", From: "2026-09-11"));
        afterSignIn.Cell(0, "value").Should().Be(0.0);
        afterSignIn.Suppressed.Should().Be(0);

        var activeAfterSignIn = await context.QueryAsync(new QueryAnalyticsQuery("login_users", From: "2026-09-11"));
        activeAfterSignIn.Cell(0, "value").Should().Be(5.0);

        var users = await context.QueryAsync(new QueryAnalyticsQuery("login_users", By: "day"));
        users.Rows.Select(row => row["day"]).Should().Equal("2026-09-10", "2026-09-11");
        users.Rows.Select(row => row["value"]).Should().Equal(6.0, 5.0);
    }

    [Fact]
    public async Task InactiveAccounts_HaveNoSignInForThirtyDays()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var dormant = await context.AddAccountAsync(createdAt: AnalyticsSeed.LongAgo);
        await context.AddRefreshTokenAsync(dormant.Id, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var active = await context.AddAccountAsync(createdAt: AnalyticsSeed.LongAgo);
        await context.AddRefreshTokenAsync(active.Id, AnalyticsSeed.Recent);
        await context.AddAccountAsync(createdAt: AnalyticsSeed.LongAgo);
        await context.AddAccountAsync(createdAt: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await context.AddAccountAsync(createdAt: AnalyticsSeed.LongAgo, deletedAt: AnalyticsSeed.Recent);
        await context.AddAccountAsync(UserRole.Teacher, createdAt: AnalyticsSeed.LongAgo);

        var result = await context.QueryAsync(new QueryAnalyticsQuery("inactive_accounts", By: "role"));

        result.Rows.Select(row => row["role"]).Should().Equal("User", "Teacher");
        result.Rows.Select(row => row["value"]).Should().Equal(2.0, 1.0);

        // On 2026-08-15 the dormant account had signed in two weeks earlier, the active one had not signed in yet
        // and the account deleted in September still existed.
        var earlier = await context.QueryAsync(new QueryAnalyticsQuery("inactive_accounts", To: "2026-08-15", Role: "User"));
        earlier.Cell(0, "value").Should().Be(3.0);
        earlier.From.Should().BeNull();
        earlier.To.Should().Be(new DateOnly(2026, 8, 15));
    }

    [Fact]
    public async Task Limit_TruncatesTheRows()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        await context.AddAccountAsync();
        await context.AddAccountAsync(UserRole.Teacher);
        await context.AddAccountAsync(UserRole.Admin);

        var result = await context.QueryAsync(new QueryAnalyticsQuery("total_accounts", By: "role", Limit: 2));

        result.Rows.Should().HaveCount(2);
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task Json_NeverContainsAccountIdsOrEmails()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var learners = await context.AddAccountsAsync(5);

        foreach (var learner in learners)
        {
            await context.AddRefreshTokenAsync(learner.Id, AnalyticsSeed.Recent);
        }

        var result = await context.QueryAsync(new QueryAnalyticsQuery("logins", By: "day,role"));

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"day\":\"2026-09-10\"").And.Contain("\"labels\":{}");
        foreach (var learner in learners)
        {
            json.Should().NotContain(learner.Id.ToString()).And.NotContain(learner.Email);
        }
    }

    [Fact]
    public async Task InvalidQuery_IsRejectedByTheHandlerToo()
    {
        await using var context = await IdentityTestContext.CreateAsync();

        var act = () => context.QueryAsync(new QueryAnalyticsQuery("password_hash"));

        (await act.Should().ThrowAsync<AppException>())
            .Which.Type.Should().Be(ErrorType.Validation);
    }
}
