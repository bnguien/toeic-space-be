using ToeicSpace.Content.Application.DTOs.Analytics;

namespace ToeicSpace.Content.Application.Analytics.Queries.GetAnalyticsCatalog;

/// <summary>The whitelisted metrics, dimensions, filters and limits of the Content analytics endpoint.</summary>
public sealed record GetAnalyticsCatalogQuery : IRequest<AnalyticsCatalogDto>;
