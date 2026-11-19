using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Classroom.API.Middlewares;
using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Classroom.Application.Common.Behaviors;
using ToeicSpace.Classroom.Application.DTOs.Analytics;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Classroom.Tests.Analytics;

/// <summary>Only catalogue metrics, dimensions, filters and values pass; every failure names the parameter.</summary>
public sealed class AnalyticsValidationTests
{
    private readonly QueryAnalyticsValidator _validator = new(new AnalyticsClock());

    [Theory]
    [InlineData("class_count", null)]
    [InlineData("CLASS_COUNT", "month,status")]
    [InlineData("class_enrollment", " week , class ")]
    [InlineData("enrollment_status_mix", "class")]
    [InlineData("teacher_load", null)]
    [InlineData("active_classes", "status,month")]
    public void CatalogueQueries_AreValid(string metric, string? by)
    {
        Assert.True(_validator.Validate(new QueryAnalyticsQuery(metric, by)).IsValid);
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("student_emails", "Unknown metric")]
    [InlineData("class_avg_score", "planned")]
    public void Metric_MustBeInTheCatalogue(string? metric, string message)
    {
        Assert.Contains(message, Assert.Single(ErrorsFor(new QueryAnalyticsQuery(metric), "metric")));
    }

    [Theory]
    [InlineData("class_count", "student", "Unknown dimension")]
    [InlineData("class_count", "class", "not supported")]
    [InlineData("class_enrollment", "class,class", "twice")]
    [InlineData("class_enrollment", "month,status,class", "At most 2")]
    [InlineData("enrollment_status_mix", "class,class", "twice")]
    [InlineData("teacher_load", "teacher,month", "not supported")]
    public void Dimensions_MustBeAllowedForTheMetric(string metric, string by, string message)
    {
        Assert.Contains(ErrorsFor(new QueryAnalyticsQuery(metric, by), "by"), error => error.Contains(message));
    }

    [Fact]
    public void RequiredDimension_CountsTowardsTheMaximum()
    {
        var errors = new List<KeyValuePair<string, string>>();
        AnalyticsRequestParser.Parse(new QueryAnalyticsQuery("enrollment_status_mix", "class"), new DateOnly(2026, 9, 14), errors);
        Assert.Empty(errors);

        Assert.Contains(
            ErrorsFor(new QueryAnalyticsQuery("teacher_load", "teacher,teacher"), "by"),
            error => error.Contains("twice"));
    }

    [Fact]
    public void Filters_MustBeAllowedForTheMetric()
    {
        Assert.Contains("Allowed filters: from, to, status",
            Assert.Single(ErrorsFor(new QueryAnalyticsQuery("class_count", ClassId: Guid.NewGuid().ToString()), "class_id")));
        Assert.Contains("takes no filters", Assert.Single(ErrorsFor(new QueryAnalyticsQuery("teacher_load", From: "2026-01-01"), "from")));
        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("enrollment_status_mix", To: "2026-01-01"), "to"));
        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("active_classes", Status: "Active"), "status"));
    }

    [Fact]
    public void StatusFilter_FollowsTheMetricSource()
    {
        Assert.True(_validator.Validate(new QueryAnalyticsQuery("class_count", Status: "completed")).IsValid);
        Assert.True(_validator.Validate(new QueryAnalyticsQuery("class_enrollment", Status: "withdrawn")).IsValid);
        Assert.True(_validator.Validate(new QueryAnalyticsQuery("class_enrollment", Status: "2")).IsValid);

        Assert.Contains("Active, Withdrawn",
            Assert.Single(ErrorsFor(new QueryAnalyticsQuery("class_enrollment", Status: "completed"), "status")));
        // Soft-deleted classes never count, so Deleted is not offered.
        Assert.Contains("Draft, Active, Completed, Archived",
            Assert.Single(ErrorsFor(new QueryAnalyticsQuery("class_count", Status: "Deleted"), "status")));
    }

    [Theory]
    [InlineData("class_id", "not-a-guid")]
    [InlineData("from", "09/01/2026")]
    [InlineData("to", "2026-13-01")]
    public void FilterValues_MustBeValid(string filter, string value)
    {
        var query = new QueryAnalyticsQuery("class_enrollment") with
        {
            ClassId = filter == "class_id" ? value : null,
            From = filter == "from" ? value : null,
            To = filter == "to" ? value : null
        };

        Assert.NotEmpty(ErrorsFor(query, filter));
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-01")]
    [InlineData("2025-01-01", "2026-09-01")]
    public void DateRange_MustBeOrderedAndAtMost366Days(string from, string to)
    {
        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("class_count", From: from, To: to), "from"));
    }

    [Fact]
    public void DateRange_OfExactly366Days_IsValid()
    {
        Assert.True(_validator.Validate(new QueryAnalyticsQuery("class_count", From: "2025-09-14", To: "2026-09-14")).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Limit_MustBeBetweenOneAnd500(int limit)
    {
        Assert.NotEmpty(ErrorsFor(new QueryAnalyticsQuery("class_count", Limit: limit), "limit"));
    }

    [Fact]
    public void Parser_AddsDefaultsAndRequiredDimension()
    {
        var errors = new List<KeyValuePair<string, string>>();
        var classId = Guid.NewGuid();

        var request = AnalyticsRequestParser.Parse(
            new QueryAnalyticsQuery("enrollment_status_mix", By: "Class", ClassId: classId.ToString()),
            new DateOnly(2026, 9, 14),
            errors);

        Assert.Empty(errors);
        Assert.Equal(["status", "class"], request!.Dimensions);
        Assert.Equal(classId, request.ClassId);
        Assert.Null(request.Range);
        Assert.Equal(100, request.Limit);

        var ranged = AnalyticsRequestParser.Parse(
            new QueryAnalyticsQuery("class_enrollment", Status: "with-drawn"), new DateOnly(2026, 9, 14), errors);
        Assert.Equal(new DateOnly(2026, 6, 17), ranged!.Range!.From);
        Assert.Equal(new DateOnly(2026, 9, 14), ranged.Range.To);
        Assert.Equal(EnrollmentStatus.Withdrawn, ranged.EnrollmentStatus);
    }

    [Fact]
    public async Task Pipeline_ReturnsEveryFailurePerParameter()
    {
        var behavior = new ValidationBehavior<QueryAnalyticsQuery, AnalyticsResultDto>([_validator]);

        var error = await Assert.ThrowsAsync<ClassRuleException>(() => behavior.Handle(
            new QueryAnalyticsQuery("class_count", By: "student", Limit: 0, ClassId: "x"),
            _ => Task.FromResult<AnalyticsResultDto>(null!),
            default));

        Assert.Equal("validation_error", error.Code);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal(["by", "class_id", "limit"], error.Errors!.Keys.Order());
    }

    [Fact]
    public async Task ExceptionHandler_WritesErrorsToTheProblem()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var error = ClassRuleException.Validation(new Dictionary<string, string[]> { ["by"] = ["Unknown dimension 'x'."] });

        await new ClassroomExceptionHandler(NullLogger<ClassroomExceptionHandler>.Instance).TryHandleAsync(context, error, default);

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("validation_error", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Unknown dimension 'x'.", json.RootElement.GetProperty("errors").GetProperty("by")[0].GetString());
    }

    [Fact]
    public void Selection_AboveTheRowCap_IsRefused()
    {
        var within = new int[AnalyticsLimits.MaxFactRows];
        var above = new int[AnalyticsLimits.MaxFactRows + 1];

        Assert.Same(within, AnalyticsExecution.EnsureWithinLimit(within));
        var error = Assert.Throws<ClassRuleException>(() => AnalyticsExecution.EnsureWithinLimit(above));
        Assert.Equal("analytics_range_too_large", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task ReadOverTheBudget_BecomesA503()
    {
        var error = await Assert.ThrowsAsync<ClassRuleException>(() => AnalyticsExecution.WithTimeoutAsync(
            async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            },
            CancellationToken.None));

        Assert.Equal("analytics_timeout", error.Code);
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task CallerCancellation_IsNotReportedAsTimeout()
    {
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AnalyticsExecution.WithTimeoutAsync(
            async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            },
            caller.Token));
    }

    private IReadOnlyList<string> ErrorsFor(QueryAnalyticsQuery query, string parameter) =>
        _validator.Validate(query).Errors
            .Where(error => error.PropertyName == parameter)
            .Select(error => error.ErrorMessage)
            .ToList();
}
