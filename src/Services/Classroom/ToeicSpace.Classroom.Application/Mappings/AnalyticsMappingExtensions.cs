using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.DTOs.Analytics;

namespace ToeicSpace.Classroom.Application.Mappings;

public static class AnalyticsMappingExtensions
{
    public static AnalyticsMetricDto ToDto(this AnalyticsMetric metric) => new(
        metric.Name,
        metric.Description,
        metric.Unit,
        metric.CountUnit,
        metric.Dimensions,
        metric.Filters,
        metric.RequiredDimension,
        metric.Visibility,
        metric.Personal,
        metric.Suppressed,
        metric.Sort.ToApiName());

    public static AnalyticsPlannedMetricDto ToDto(this PlannedAnalyticsMetric metric) => new(
        metric.Name,
        metric.Service,
        metric.Description,
        metric.Visibility);

    public static IReadOnlyList<AnalyticsNameDto> ToNameDtos(this IReadOnlyDictionary<string, string> descriptions) =>
        [.. descriptions.Select(entry => new AnalyticsNameDto(entry.Key, entry.Value))];

    public static string ToApiName(this AnalyticsSort sort) => sort switch
    {
        AnalyticsSort.ValueAscending => "value_asc",
        AnalyticsSort.ValueDescending => "value_desc",
        _ => "dimensions"
    };
}
