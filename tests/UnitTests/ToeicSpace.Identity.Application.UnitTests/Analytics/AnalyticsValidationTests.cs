using ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.UnitTests.Analytics;

/// <summary>Only catalogue metrics, dimensions, filters and values pass; every failure names the parameter.</summary>
public sealed class AnalyticsValidationTests
{
    private static readonly DateOnly Today = new(2026, 9, 16);

    private readonly QueryAnalyticsValidator _validator = new(new FixedTimeProvider());

    [Theory]
    [InlineData("new_registrations", null)]
    [InlineData("LOGINS", "week,role")]
    [InlineData("login_users", " month , role ")]
    [InlineData("total_accounts", "role,status")]
    [InlineData("account_role_mix", null)]
    [InlineData("oauth_accounts", "provider")]
    [InlineData("inactive_accounts", "role")]
    [InlineData("teacher_count", "status")]
    public void CatalogueQueries_AreValid(string metric, string? by)
    {
        _validator.Validate(new QueryAnalyticsQuery(metric, by)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("password_hash", "Unknown metric")]
    [InlineData("attempt_count", "new_registrations")]
    public void Metric_MustBeInTheCatalogue(string? metric, string message)
    {
        ErrorsFor(new QueryAnalyticsQuery(metric), "metric").Should().ContainSingle()
            .Which.Should().Contain(message);
    }

    [Theory]
    [InlineData("logins", "user_id", "Unknown dimension")]
    [InlineData("logins", "email", "Unknown dimension")]
    [InlineData("total_accounts", "day", "not supported")]
    [InlineData("account_role_mix", "status", "not supported")]
    [InlineData("logins", "day,day", "twice")]
    [InlineData("logins", "day,role,week", "At most 2")]
    public void Dimensions_MustBeAllowedForTheMetric(string metric, string by, string message)
    {
        ErrorsFor(new QueryAnalyticsQuery(metric, by), "by").Should().Contain(error => error.Contains(message));
    }

    [Fact]
    public void Filters_MustBeAllowedForTheMetric()
    {
        ErrorsFor(new QueryAnalyticsQuery("logins", Status: "Active"), "status").Should().ContainSingle()
            .Which.Should().Contain("Allowed filters: from, to, role");

        ErrorsFor(new QueryAnalyticsQuery("teacher_count", Role: "Admin"), "role").Should().ContainSingle()
            .Which.Should().Contain("takes no filters");

        // Snapshots only take the snapshot date.
        ErrorsFor(new QueryAnalyticsQuery("total_accounts", From: "2026-01-01"), "from").Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("role", "superuser")]
    [InlineData("status", "banned")]
    [InlineData("provider", "facebook")]
    [InlineData("from", "09/01/2026")]
    [InlineData("to", "2026-13-01")]
    public void FilterValues_MustBeValid(string filter, string value)
    {
        var metric = filter switch
        {
            "status" => "total_accounts",
            "provider" => "oauth_accounts",
            _ => "new_registrations"
        };

        var query = new QueryAnalyticsQuery(metric) with
        {
            Role = filter == "role" ? value : null,
            Status = filter == "status" ? value : null,
            Provider = filter == "provider" ? value : null,
            From = filter == "from" ? value : null,
            To = filter == "to" ? value : null
        };

        ErrorsFor(query, filter).Should().ContainSingle().Which.Should().Contain("'");
    }

    [Fact]
    public void BadEnumValue_ListsTheAllowedValues()
    {
        ErrorsFor(new QueryAnalyticsQuery("account_status_mix", Role: "owner"), "role").Should().ContainSingle()
            .Which.Should().Contain("User, Admin, Teacher");
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-01")]
    [InlineData("2025-01-01", "2026-09-01")]
    public void DateRange_MustBeOrderedAndAtMost366Days(string from, string to)
    {
        ErrorsFor(new QueryAnalyticsQuery("logins", From: from, To: to), "from").Should().NotBeEmpty();
    }

    [Fact]
    public void DateRange_OfExactly366Days_IsValid()
    {
        _validator.Validate(new QueryAnalyticsQuery("logins", From: "2025-09-16", To: "2026-09-16"))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Limit_MustBeBetweenOneAnd500(int limit)
    {
        ErrorsFor(new QueryAnalyticsQuery("logins", Limit: limit), "limit").Should().NotBeEmpty();
    }

    [Fact]
    public void Parser_AddsDefaultsAndRequiredDimension()
    {
        var errors = new List<KeyValuePair<string, string>>();

        var mix = AnalyticsRequestParser.Parse(new QueryAnalyticsQuery("account_status_mix", Role: "student"), Today, errors);
        var logins = AnalyticsRequestParser.Parse(new QueryAnalyticsQuery("logins", By: "Week"), Today, errors);
        var total = AnalyticsRequestParser.Parse(new QueryAnalyticsQuery("total_accounts"), Today, errors);
        var teachers = AnalyticsRequestParser.Parse(new QueryAnalyticsQuery("teacher_count"), Today, errors);

        errors.Should().BeEmpty();

        mix!.Dimensions.Should().Equal("status");
        mix.Role.Should().Be(UserRole.User);
        mix.Range.Should().BeNull();
        mix.Limit.Should().Be(100);

        logins!.Dimensions.Should().Equal("week");
        logins.Range!.From.Should().Be(new DateOnly(2026, 6, 19));
        logins.Range.To.Should().Be(Today);

        total!.Range!.From.Should().Be(Today);
        total.Range.To.Should().Be(Today);

        teachers!.Role.Should().Be(UserRole.Teacher);
    }

    private IReadOnlyList<string> ErrorsFor(QueryAnalyticsQuery query, string parameter)
        => _validator.Validate(query).Errors
            .Where(error => error.PropertyName == parameter)
            .Select(error => error.ErrorMessage)
            .ToList();
}
