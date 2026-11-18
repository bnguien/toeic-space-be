using ToeicSpace.Identity.Application.Features.Analytics.Results;

namespace ToeicSpace.Identity.Application.Features.Analytics.Queries.GetAnalyticsCatalog;

/// <summary>The whitelisted metrics, dimensions, filters and limits of the Identity analytics endpoint.</summary>
public sealed record GetAnalyticsCatalogQuery : IRequest<AnalyticsCatalogDto>;
