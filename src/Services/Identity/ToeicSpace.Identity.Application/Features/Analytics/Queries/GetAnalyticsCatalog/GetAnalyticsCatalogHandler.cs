using ToeicSpace.Identity.Application.Features.Analytics.Common;
using ToeicSpace.Identity.Application.Features.Analytics.Results;

namespace ToeicSpace.Identity.Application.Features.Analytics.Queries.GetAnalyticsCatalog;

public sealed class GetAnalyticsCatalogHandler : IRequestHandler<GetAnalyticsCatalogQuery, AnalyticsCatalogDto>
{
    private static readonly AnalyticsCatalogDto Catalog = Build();

    public Task<AnalyticsCatalogDto> Handle(
        GetAnalyticsCatalogQuery request,
        CancellationToken cancellationToken)
        => Task.FromResult(Catalog);

    private static AnalyticsCatalogDto Build()
        => new(
            Service: "identity",
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
            Limits: new AnalyticsLimitsDto(
                AnalyticsLimits.MinimumAccountsPerGroup,
                AnalyticsLimits.MaxDimensions,
                AnalyticsLimits.DefaultRowLimit,
                AnalyticsLimits.MaxRowLimit,
                AnalyticsLimits.DefaultRangeDays,
                AnalyticsLimits.MaxRangeDays,
                AnalyticsLimits.InactivityDays,
                (int)AnalyticsLimits.QueryTimeout.TotalSeconds));
}
