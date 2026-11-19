using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsCatalog;

public sealed class GetAnalyticsCatalogHandler : IRequestHandler<GetAnalyticsCatalogQuery, AnalyticsCatalogDto>
{
    private static readonly AnalyticsCatalogDto Catalog = Build();

    public Task<AnalyticsCatalogDto> Handle(
        GetAnalyticsCatalogQuery request,
        CancellationToken cancellationToken)
        => Task.FromResult(Catalog);

    private static AnalyticsCatalogDto Build()
        => new(
            Metrics: [.. AnalyticsCatalog.Metrics.Select(metric => new AnalyticsMetricDto(
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
                metric.Sort switch
                {
                    AnalyticsSort.ValueAscending => "value_asc",
                    AnalyticsSort.ValueDescending => "value_desc",
                    _ => "dimensions"
                }))],
            Dimensions: [.. AnalyticsDimensions.Descriptions.Select(entry => new AnalyticsNameDto(entry.Key, entry.Value))],
            Filters: [.. AnalyticsFilters.Descriptions.Select(entry => new AnalyticsNameDto(entry.Key, entry.Value))],
            EventTypes: AnalyticsEventTypes.All,
            Limits: new AnalyticsLimitsDto(
                AnalyticsLimits.MinimumLearnersPerGroup,
                AnalyticsLimits.MaxDimensions,
                AnalyticsLimits.DefaultRowLimit,
                AnalyticsLimits.MaxRowLimit,
                AnalyticsLimits.DefaultEventLimit,
                AnalyticsLimits.MaxEventLimit,
                AnalyticsLimits.DefaultRangeDays,
                AnalyticsLimits.MaxRangeDays,
                (int)AnalyticsLimits.QueryTimeout.TotalSeconds),
            Planned: [.. AnalyticsCatalog.Planned.Select(metric => new AnalyticsPlannedMetricDto(
                metric.Name,
                metric.Service,
                metric.Description,
                metric.Visibility))]);
}
