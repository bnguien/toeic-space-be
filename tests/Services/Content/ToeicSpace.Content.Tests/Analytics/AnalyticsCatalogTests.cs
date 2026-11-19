using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Analytics.Queries.GetAnalyticsCatalog;
using Xunit;

namespace ToeicSpace.Content.Tests.Analytics;

/// <summary>The catalogue is the whitelist: complete, self-describing and consistent.</summary>
public sealed class AnalyticsCatalogTests
{
    private static readonly string[] ExpectedMetrics =
    [
        "course_count", "lesson_count", "study_sessions", "study_time", "active_learners", "vocabulary_reviews",
        "vocabulary_review_accuracy", "vocabulary_review_retention", "vocabulary_words_learned", "saved_words",
        "dictation_attempts", "dictation_completion", "grammar_attempts", "grammar_accuracy",
        "grammar_lesson_completion", "video_watches", "video_watch_time", "news_reads", "news_read_completion"
    ];

    /// <summary>The content metrics the Assessment catalogue listed as planned for this service.</summary>
    private static readonly string[] FormerlyPlanned =
    [
        "study_time", "grammar_accuracy", "grammar_lesson_completion", "dictation_completion",
        "vocabulary_words_learned", "vocabulary_review_retention", "video_watch_time", "news_reads"
    ];

    [Fact]
    public void Catalogue_HoldsEveryContentMetric_Once()
    {
        var names = AnalyticsCatalog.Metrics.Select(metric => metric.Name).ToList();

        Assert.Equal(ExpectedMetrics.Order(), names.Order());
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(FormerlyPlanned, name => Assert.NotNull(AnalyticsCatalog.Find(name)));
    }

    [Fact]
    public void EveryDimensionAndFilter_IsDescribed()
    {
        foreach (var metric in AnalyticsCatalog.Metrics)
        {
            Assert.All(metric.Dimensions, dimension => Assert.True(AnalyticsDimensions.Descriptions.ContainsKey(dimension),
                $"{metric.Name}: dimension '{dimension}' has no description."));
            Assert.All(metric.Filters, filter => Assert.True(AnalyticsFilters.Descriptions.ContainsKey(filter),
                $"{metric.Name}: filter '{filter}' has no description."));

            if (metric.RequiredDimension is { } required)
            {
                Assert.Contains(required, metric.Dimensions);
            }

            Assert.False(string.IsNullOrWhiteSpace(metric.Description));
            Assert.Equal(AnalyticsVisibility.Admin, metric.Visibility);
            Assert.Equal(metric.UsesDateRange, metric.Filters.Contains(AnalyticsFilters.To));
        }
    }

    [Fact]
    public void OnlyCatalogueContentMetrics_AreNotSuppressed()
    {
        var unsuppressed = AnalyticsCatalog.Metrics.Where(metric => !metric.Suppressed).Select(metric => metric.Name);

        Assert.Equal(["course_count", "lesson_count"], unsuppressed.Order());
    }

    [Fact]
    public void EverySource_IsUsedByAMetric()
    {
        var used = AnalyticsCatalog.Metrics.Select(metric => metric.Source).ToHashSet();

        Assert.All(Enum.GetValues<AnalyticsSource>(), source => Assert.Contains(source, used));
    }

    [Fact]
    public void PlannedMetrics_AreNotServed()
    {
        Assert.All(AnalyticsCatalog.Planned, planned =>
        {
            Assert.Null(AnalyticsCatalog.Find(planned.Name));
            Assert.True(AnalyticsCatalog.IsPlanned(planned.Name));
        });
    }

    [Fact]
    public async Task CatalogEndpoint_ListsEverything()
    {
        var catalog = await new GetAnalyticsCatalogHandler().Handle(new GetAnalyticsCatalogQuery(), CancellationToken.None);

        Assert.Equal(AnalyticsCatalog.Metrics.Count, catalog.Metrics.Count);
        Assert.Equal(AnalyticsDimensions.Descriptions.Count, catalog.Dimensions.Count);
        Assert.Equal(AnalyticsFilters.Descriptions.Count, catalog.Filters.Count);
        Assert.Equal(5, catalog.Limits.MinimumLearnersPerGroup);
        Assert.Equal(366, catalog.Limits.MaxRangeDays);
        Assert.Equal(10, catalog.Limits.TimeoutSeconds);

        var lessons = catalog.Metrics.Single(metric => metric.Name == "lesson_count");
        Assert.Equal("course", lessons.RequiredDimension);
        Assert.Equal("dimensions", lessons.Sort);
        Assert.Contains(catalog.Planned, planned => planned.Name == "course_lesson_progress");
    }
}
