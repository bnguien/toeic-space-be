using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.DTOs.Analytics;

namespace ToeicSpace.Content.Application.Mappings;

public static class AnalyticsMappingExtensions
{
    public static AnalyticsMetricDto ToDto(this AnalyticsMetric metric) =>
        new(metric.Name, metric.Description, metric.Unit, metric.CountUnit, metric.Dimensions, metric.Filters,
            metric.RequiredDimension, metric.Visibility, metric.Personal, metric.Suppressed, metric.Sort.ToName());

    public static AnalyticsPlannedMetricDto ToDto(this PlannedAnalyticsMetric metric) =>
        new(metric.Name, metric.Service, metric.Description, metric.Visibility);

    public static AnalyticsNameDto ToNameDto(this KeyValuePair<string, string> entry) =>
        new(entry.Key, entry.Value);

    public static string ToName(this AnalyticsSort sort) => sort switch
    {
        AnalyticsSort.ValueAscending => "value_asc",
        AnalyticsSort.ValueDescending => "value_desc",
        _ => "dimensions"
    };
}
