using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;

/// <summary>A page of anonymised attempt or answer events, ordered by attempt start time.</summary>
public sealed record GetAnalyticsEventsQuery(
    string? Type,
    string? From = null,
    string? To = null,
    string? Cursor = null,
    int? Limit = null) : IRequest<AnalyticsEventsDto>;

public static class AnalyticsEventTypes
{
    public const string Attempt = "attempt";
    public const string Answer = "answer";

    public static readonly IReadOnlyList<string> All = [Attempt, Answer];
}
