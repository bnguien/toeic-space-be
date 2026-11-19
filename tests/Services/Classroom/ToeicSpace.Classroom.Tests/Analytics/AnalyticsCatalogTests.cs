using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Classroom.API.Controllers;
using ToeicSpace.Classroom.API.Security;
using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.Analytics.Queries.GetAnalyticsCatalog;
using Xunit;

namespace ToeicSpace.Classroom.Tests.Analytics;

/// <summary>The catalogue is complete, self-consistent and only reachable by admins.</summary>
public sealed class AnalyticsCatalogTests
{
    [Fact]
    public void Catalogue_ListsEveryClassroomMetric()
    {
        Assert.Equal(
            ["class_count", "active_classes", "class_enrollment", "enrollment_status_mix", "enrolled_learners",
                "withdraw_rate", "class_size_avg", "teacher_load"],
            AnalyticsCatalog.Metrics.Select(metric => metric.Name));
    }

    [Fact]
    public void EveryMetric_UsesKnownDimensionsAndFilters()
    {
        foreach (var metric in AnalyticsCatalog.Metrics)
        {
            Assert.False(string.IsNullOrWhiteSpace(metric.Description));
            Assert.Equal(AnalyticsVisibility.Admin, metric.Visibility);
            Assert.All(metric.Dimensions, dimension => Assert.True(AnalyticsDimensions.Descriptions.ContainsKey(dimension)));
            Assert.All(metric.Filters, filter => Assert.True(AnalyticsFilters.Descriptions.ContainsKey(filter)));
            Assert.Equal(metric.Filters.Contains(AnalyticsFilters.From), metric.Filters.Contains(AnalyticsFilters.To));

            if (metric.RequiredDimension is { } required)
            {
                Assert.Contains(required, metric.Dimensions);
            }
        }
    }

    [Theory]
    [InlineData("enrolled_learners", true)]
    [InlineData("withdraw_rate", true)]
    [InlineData("class_size_avg", true)]
    [InlineData("class_count", false)]
    [InlineData("class_enrollment", false)]
    [InlineData("teacher_load", false)]
    public void LearnerMetrics_AreSuppressed(string name, bool suppressed)
    {
        Assert.Equal(suppressed, AnalyticsCatalog.Find(name)!.Suppressed);
    }

    [Fact]
    public async Task CatalogueEndpoint_ReturnsMetricsLimitsAndPlanned()
    {
        var catalog = await new GetAnalyticsCatalogHandler().Handle(new GetAnalyticsCatalogQuery(), default);

        Assert.Equal(AnalyticsCatalog.Metrics.Count, catalog.Metrics.Count);
        Assert.Empty(catalog.EventTypes);
        Assert.Equal(5, catalog.Limits.MinimumLearnersPerGroup);
        Assert.Equal(366, catalog.Limits.MaxRangeDays);
        Assert.Equal(10, catalog.Limits.TimeoutSeconds);
        Assert.Equal("value_desc", catalog.Metrics.Single(metric => metric.Name == "teacher_load").Sort);
        Assert.Equal("class_avg_score", Assert.Single(catalog.Planned).Name);
        Assert.Contains(catalog.Filters, filter => filter.Name == "class_id");
    }

    [Fact]
    public void AnalyticsController_RequiresTheAdminOnlyReaderPolicy()
    {
        var authorize = Assert.Single(typeof(AnalyticsController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(AuthorizationPolicies.AnalyticsReader, authorize.Policy);
        Assert.Equal(["Admin"], AuthorizationPolicies.AnalyticsReaderRoles);
        Assert.DoesNotContain(typeof(AnalyticsController).GetMethods(), method =>
            method.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Length > 0);
    }
}
