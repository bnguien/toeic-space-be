using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.DTOs.Analytics;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;

public sealed class QueryAnalyticsHandler : IRequestHandler<QueryAnalyticsQuery, AnalyticsResultDto>
{
    private readonly IClassroomAnalyticsReader _reader;
    private readonly TimeProvider _timeProvider;

    public QueryAnalyticsHandler(
        IClassroomAnalyticsReader reader,
        TimeProvider timeProvider)
    {
        _reader = reader;
        _timeProvider = timeProvider;
    }

    public async Task<AnalyticsResultDto> Handle(
        QueryAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var errors = new List<KeyValuePair<string, string>>();

        var request = AnalyticsRequestParser.Parse(query, DateOnly.FromDateTime(now), errors)
            ?? throw ClassRuleException.Validation(AnalyticsValues.ToErrorDictionary(errors));

        return await AnalyticsExecution.WithTimeoutAsync(async token =>
        {
            var groups = await ComputeAsync(request, token);
            var selection = AnalyticsResultBuilder.Select(request, groups);
            var labels = await LoadLabelsAsync(request, selection, token);

            return AnalyticsResultBuilder.ToDto(request, selection, labels, now);
        }, cancellationToken);
    }

    private Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken)
        => request.Metric.Source switch
        {
            AnalyticsSource.Classes => ClassMetrics.ComputeAsync(_reader, request, cancellationToken),
            AnalyticsSource.Teachers => ClassMetrics.ComputeTeacherLoadAsync(_reader, request, cancellationToken),
            AnalyticsSource.Enrollments => EnrollmentMetrics.ComputeAsync(_reader, request, cancellationToken),
            AnalyticsSource.ClassCohorts => EnrollmentMetrics.ComputeCohortsAsync(_reader, request, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown analytics source '{request.Metric.Source}'.")
        };

    /// <summary>Codes of the classes in the rows, so callers can read them without another call. Teachers get none.</summary>
    private async Task<IReadOnlyDictionary<string, string>?> LoadLabelsAsync(
        AnalyticsRequest request,
        AnalyticsSelection selection,
        CancellationToken cancellationToken)
    {
        var classIds = AnalyticsResultBuilder.IdsOf(request, selection, AnalyticsDimensions.Class);

        if (classIds.Count == 0)
        {
            return null;
        }

        var codes = await _reader.GetClassCodesAsync(classIds, cancellationToken);

        return codes.ToDictionary(entry => entry.Key.ToString("D"), entry => entry.Value);
    }
}
