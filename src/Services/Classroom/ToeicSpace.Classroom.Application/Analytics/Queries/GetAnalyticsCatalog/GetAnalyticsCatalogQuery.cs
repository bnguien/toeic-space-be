using ToeicSpace.Classroom.Application.DTOs.Analytics;

namespace ToeicSpace.Classroom.Application.Analytics.Queries.GetAnalyticsCatalog;

/// <summary>The whitelisted metrics, dimensions, filters and limits of the analytics endpoint.</summary>
public sealed record GetAnalyticsCatalogQuery : IRequest<AnalyticsCatalogDto>;
