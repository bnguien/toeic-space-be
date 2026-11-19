using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

/// <summary>
/// Raw query-string values of <c>GET api/v1/analytics/query</c>. Everything is a string so unknown values
/// are reported with a clear message; <see cref="AnalyticsRequestParser"/> turns them into a typed request.
/// </summary>
public sealed record QueryAnalyticsQuery(
    string? Metric,
    string? By = null,
    string? From = null,
    string? To = null,
    string? TestId = null,
    string? Part = null,
    string? Section = null,
    string? Difficulty = null,
    string? Status = null,
    string? Mode = null,
    int? Limit = null) : IRequest<AnalyticsResultDto>;
