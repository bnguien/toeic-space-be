using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.DTOs.Analytics;
using ToeicSpace.Classroom.Application.Mappings;

namespace ToeicSpace.Classroom.Application.Analytics.Queries.GetAnalyticsCatalog;

public sealed class GetAnalyticsCatalogHandler : IRequestHandler<GetAnalyticsCatalogQuery, AnalyticsCatalogDto>
{
    private static readonly AnalyticsCatalogDto Catalog = Build();

    public Task<AnalyticsCatalogDto> Handle(
        GetAnalyticsCatalogQuery request,
        CancellationToken cancellationToken)
        => Task.FromResult(Catalog);

    private static AnalyticsCatalogDto Build()
        => new(
            Metrics: [.. AnalyticsCatalog.Metrics.Select(metric => metric.ToDto())],
            Dimensions: AnalyticsDimensions.Descriptions.ToNameDtos(),
            Filters: AnalyticsFilters.Descriptions.ToNameDtos(),
            EventTypes: [],
            Limits: new AnalyticsLimitsDto(
                AnalyticsLimits.MinimumLearnersPerGroup,
                AnalyticsLimits.MaxDimensions,
                AnalyticsLimits.DefaultRowLimit,
                AnalyticsLimits.MaxRowLimit,
                AnalyticsLimits.DefaultRangeDays,
                AnalyticsLimits.MaxRangeDays,
                (int)AnalyticsLimits.QueryTimeout.TotalSeconds),
            Planned: [.. AnalyticsCatalog.Planned.Select(metric => metric.ToDto())]);
}
