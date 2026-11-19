using ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;
using ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

namespace ToeicSpace.Assessment.Application.UnitTests.Analytics;

/// <summary>Only catalogue metrics, dimensions, filters and values pass; every failure names the parameter.</summary>
public class AnalyticsValidationTests
{
    private readonly QueryAnalyticsValidator _validator = new(new FixedTimeProvider());
    private readonly GetAnalyticsEventsValidator _events = new(new FixedTimeProvider());

    [Theory]
    [InlineData("attempt_count", null)]
    [InlineData("ACCURACY", "part,difficulty")]
    [InlineData("accuracy", " month , section ")]
    [InlineData("question_p_value", "part")]
    [InlineData("question_count", "status,test")]
    public void CatalogueQueries_AreValid(string metric, string? by)
    {
        _validator.Validate(new QueryAnalyticsQuery(metric, by)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("password_hash", "Unknown metric")]
    [InlineData("class_avg_score", "planned")]
    public void Metric_MustBeInTheCatalogue(string? metric, string message)
    {
        ErrorsFor(new QueryAnalyticsQuery(metric), "metric").Should().ContainSingle()
            .Which.Should().Contain(message);
    }

    [Theory]
    [InlineData("accuracy", "user_id", "Unknown dimension")]
    [InlineData("attempt_count", "part", "not supported")]
    [InlineData("accuracy", "part,part", "twice")]
    [InlineData("accuracy", "part,section,month", "At most 2")]
    [InlineData("question_p_value", "part,difficulty", "always groups by 'question'")]
    public void Dimensions_MustBeAllowedForTheMetric(string metric, string by, string message)
    {
        ErrorsFor(new QueryAnalyticsQuery(metric, by), "by").Should().Contain(error => error.Contains(message));
    }

    [Fact]
    public void Filters_MustBeAllowedForTheMetric()
    {
        ErrorsFor(new QueryAnalyticsQuery("attempt_count", Part: "5"), "part").Should().ContainSingle()
            .Which.Should().Contain("Allowed filters: from, to, test_id, mode, status");

        ErrorsFor(new QueryAnalyticsQuery("test_count", From: "2026-01-01"), "from").Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("part", "8")]
    [InlineData("part", "Part0")]
    [InlineData("section", "speaking")]
    [InlineData("difficulty", "impossible")]
    [InlineData("mode", "exam")]
    [InlineData("test_id", "not-a-guid")]
    [InlineData("from", "09/01/2026")]
    public void FilterValues_MustBeValid(string filter, string value)
    {
        var query = new QueryAnalyticsQuery("accuracy") with
        {
            Part = filter == "part" ? value : null,
            Section = filter == "section" ? value : null,
            Difficulty = filter == "difficulty" ? value : null,
            Mode = filter == "mode" ? value : null,
            TestId = filter == "test_id" ? value : null,
            From = filter == "from" ? value : null
        };

        ErrorsFor(query, filter).Should().NotBeEmpty();
    }

    [Fact]
    public void StatusFilter_FollowsTheMetricSource()
    {
        _validator.Validate(new QueryAnalyticsQuery("attempt_count", Status: "completed")).IsValid.Should().BeTrue();
        _validator.Validate(new QueryAnalyticsQuery("question_count", Status: "Active")).IsValid.Should().BeTrue();

        ErrorsFor(new QueryAnalyticsQuery("question_count", Status: "completed"), "status").Should()
            .ContainSingle().Which.Should().Contain("Draft, Active, Archived");
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-01")]
    [InlineData("2025-01-01", "2026-09-01")]
    public void DateRange_MustBeOrderedAndAtMost366Days(string from, string to)
    {
        ErrorsFor(new QueryAnalyticsQuery("attempt_count", From: from, To: to), "from").Should().NotBeEmpty();
    }

    [Fact]
    public void DateRange_OfExactly366Days_IsValid()
    {
        _validator.Validate(new QueryAnalyticsQuery("attempt_count", From: "2025-09-14", To: "2026-09-14"))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Limit_MustBeBetweenOneAnd500(int limit)
    {
        ErrorsFor(new QueryAnalyticsQuery("attempt_count", Limit: limit), "limit").Should().NotBeEmpty();
    }

    [Fact]
    public void Parser_AddsDefaultsAndRequiredDimension()
    {
        var errors = new List<KeyValuePair<string, string>>();

        var request = AnalyticsRequestParser.Parse(
            new QueryAnalyticsQuery("most_missed_questions", By: "Difficulty", Part: "part5", Difficulty: "very_hard"),
            new DateOnly(2026, 9, 14),
            errors);

        errors.Should().BeEmpty();
        request!.Dimensions.Should().Equal("question", "difficulty");
        request.Part.Should().Be(ToeicPart.Part5);
        request.Difficulty.Should().Be(QuestionDifficulty.VeryHard);
        request.Limit.Should().Be(100);
        request.Range!.From.Should().Be(new DateOnly(2026, 6, 17));
    }

    [Theory]
    [InlineData(null, "type")]
    [InlineData("user", "type")]
    [InlineData("attempt", "limit", 1001)]
    [InlineData("attempt", "cursor", null, "garbage!")]
    public void Events_ValidateTypeLimitAndCursor(string? type, string parameter, int? limit = null, string? cursor = null)
    {
        _events.Validate(new GetAnalyticsEventsQuery(type, Limit: limit, Cursor: cursor)).Errors
            .Should().Contain(error => error.PropertyName == parameter);
    }

    [Fact]
    public void Events_CursorOfAnotherType_IsRejected()
    {
        var cursor = new AnalyticsCursor("answer", new DateTime(2026, 9, 1), Guid.NewGuid()).Encode();

        _events.Validate(new GetAnalyticsEventsQuery("attempt", Cursor: cursor)).Errors
            .Should().Contain(error => error.PropertyName == "cursor" && error.ErrorMessage.Contains("answer"));
    }

    private IReadOnlyList<string> ErrorsFor(QueryAnalyticsQuery query, string parameter)
        => _validator.Validate(query).Errors
            .Where(error => error.PropertyName == parameter)
            .Select(error => error.ErrorMessage)
            .ToList();
}
