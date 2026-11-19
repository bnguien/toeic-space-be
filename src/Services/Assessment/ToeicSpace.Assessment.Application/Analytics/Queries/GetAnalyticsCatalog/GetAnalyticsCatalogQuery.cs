using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsCatalog;

/// <summary>The whitelisted metrics, dimensions, filters and limits of the analytics endpoints.</summary>
public sealed record GetAnalyticsCatalogQuery : IRequest<AnalyticsCatalogDto>;
