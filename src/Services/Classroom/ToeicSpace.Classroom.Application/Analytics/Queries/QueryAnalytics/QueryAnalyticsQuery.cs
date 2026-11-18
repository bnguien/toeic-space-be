using ToeicSpace.Classroom.Application.DTOs.Analytics;

namespace ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;

/// <summary>
/// Raw query-string values of <c>GET api/v1/analytics/query</c>. Everything is a string so unknown values
/// are reported with a clear message; <see cref="AnalyticsRequestParser"/> turns them into a typed request.
/// </summary>
public sealed record QueryAnalyticsQuery(
    string? Metric,
    string? By = null,
    string? From = null,
    string? To = null,
    string? Status = null,
    string? ClassId = null,
    int? Limit = null) : IRequest<AnalyticsResultDto>;
