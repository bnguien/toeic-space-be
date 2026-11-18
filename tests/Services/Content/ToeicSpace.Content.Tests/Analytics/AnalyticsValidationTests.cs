using ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Content.Application.Common.Behaviors;
using ToeicSpace.Content.Application.DTOs.Analytics;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Tests.Vocabulary;
using Xunit;

namespace ToeicSpace.Content.Tests.Analytics;

/// <summary>Only catalogue metrics, dimensions, filters and values pass; every failure names the parameter.</summary>
public sealed class AnalyticsValidationTests
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));

    private readonly QueryAnalyticsValidator _validator = new(Clock);

    [Theory]
    [InlineData("study_time", null)]
    [InlineData("STUDY_SESSIONS", "week,kind")]
    [InlineData("vocabulary_review_accuracy", " month , stage ")]
    [InlineData("lesson_count", "status")]
    [InlineData("course_count", "month,exam_type")]
    [InlineData("dictation_completion", "course")]
    [InlineData("grammar_accuracy", "document")]
    [InlineData("news_read_completion", "article")]
    public void CatalogueQueries_AreValid(string metric, string? by)
    {
        Assert.True(_validator.Validate(new QueryAnalyticsQuery(metric, by)).IsValid);
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("learner_emails", "Unknown metric")]
    [InlineData("course_lesson_progress", "planned")]
    public void Metric_MustBeInTheCatalogue(string? metric, string message)
    {
        Assert.Contains(message, Assert.Single(ErrorsFor(new QueryAnalyticsQuery(metric), "metric")));
    }

    [Theory]
    [InlineData("study_time", "learner", "Unknown dimension")]
    [InlineData("study_time", "stage", "not supported")]
    [InlineData("news_reads", "day,day", "twice")]
    [InlineData("dictation_attempts", "day,week,course", "At most 2")]
    [InlineData("dictation_completion", "day", "Allowed: month, course")]
    public void Dimensions_MustBeAllowedForTheMetric(string metric, string by, string message)
    {
        Assert.Contains(ErrorsFor(new QueryAnalyticsQuery(metric, by), "by"), error => error.Contains(message));
    }

    [Fact]
    public void Filters_MustBeAllowedForTheMetric()
    {
        Assert.Contains("Allowed filters: from, to",
            Assert.Single(ErrorsFor(new QueryAnalyticsQuery("study_time", CourseId: "3"), "course_id")));

        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("course_count", From: "2026-01-01"), "from"));
        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("news_reads", Status: "Published"), "status"));
    }

    [Fact]
    public void StatusAndCourseFilters_FollowTheMetricSource()
    {
        Assert.True(_validator.Validate(new QueryAnalyticsQuery("course_count", Status: "archived", Level: "2")).IsValid);
        Assert.True(_validator.Validate(new QueryAnalyticsQuery("lesson_count", Status: "Draft",
            CourseId: Guid.NewGuid().ToString())).IsValid);
        Assert.True(_validator.Validate(new QueryAnalyticsQuery("dictation_attempts", CourseId: "12")).IsValid);

        Assert.Contains("Draft, Published",
            Assert.Single(ErrorsFor(new QueryAnalyticsQuery("lesson_count", Status: "Archived"), "status")));
        Assert.Contains("GUID", Assert.Single(ErrorsFor(new QueryAnalyticsQuery("lesson_count", CourseId: "12"), "course_id")));
        Assert.Contains("positive integer",
            Assert.Single(ErrorsFor(new QueryAnalyticsQuery("dictation_completion", CourseId: Guid.NewGuid().ToString()), "course_id")));
        Assert.Contains("Beginner, Intermediate, Advanced",
            Assert.Single(ErrorsFor(new QueryAnalyticsQuery("course_count", Level: "expert"), "level")));
    }

    [Theory]
    [InlineData("2026-11-10", "2026-11-01")]
    [InlineData("2025-01-01", "2026-11-01")]
    [InlineData("11/01/2026", null)]
    public void DateRange_MustBeOrderedIsoAndAtMost366Days(string from, string? to)
    {
        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("news_reads", From: from, To: to), "from"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Limit_MustBeBetweenOneAnd500(int limit)
    {
        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("news_reads", Limit: limit), "limit"));
    }

    [Fact]
    public void Parser_AddsDefaultsAndRequiredDimension()
    {
        var errors = new List<KeyValuePair<string, string>>();
        var courseId = Guid.NewGuid();

        var request = AnalyticsRequestParser.Parse(
            new QueryAnalyticsQuery("lesson_count", By: "Status", Status: "published", CourseId: courseId.ToString()),
            new DateOnly(2026, 11, 14),
            errors);

        Assert.Empty(errors);
        Assert.Equal(["course", "status"], request!.Dimensions);
        Assert.Equal(LessonStatus.Published, request.LessonStatus);
        Assert.Equal(courseId, request.CourseId);
        Assert.Null(request.Range);
        Assert.Equal(100, request.Limit);

        var ranged = AnalyticsRequestParser.Parse(new QueryAnalyticsQuery("study_time"), new DateOnly(2026, 11, 14), errors);
        Assert.Equal(new DateOnly(2026, 8, 17), ranged!.Range!.From);
        Assert.Equal(new DateOnly(2026, 11, 14), ranged.Range.To);
    }

    [Fact]
    public async Task ValidationPipeline_ReportsEveryFailingParameter()
    {
        var behavior = new ValidationBehavior<QueryAnalyticsQuery, AnalyticsResultDto>([_validator]);

        var error = await Assert.ThrowsAsync<ContentRuleException>(() => behavior.Handle(
            new QueryAnalyticsQuery("study_time", By: "stage", CourseId: "1", Limit: 900),
            _ => Task.FromResult<AnalyticsResultDto>(null!),
            CancellationToken.None));

        Assert.Equal("validation_error", error.Code);
        Assert.Equal(400, error.StatusCode);
        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(error.Extensions!["errors"]);
        Assert.Equal(["by", "course_id", "limit"], errors.Keys.Order());
    }

    private IReadOnlyList<string> ErrorsFor(QueryAnalyticsQuery query, string parameter)
        => _validator.Validate(query).Errors
            .Where(error => error.PropertyName == parameter)
            .Select(error => error.ErrorMessage)
            .ToList();
}
