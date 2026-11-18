using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

public sealed class QueryAnalyticsHandler : IRequestHandler<QueryAnalyticsQuery, AnalyticsResultDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly TimeProvider _timeProvider;

    public QueryAnalyticsHandler(
        IAssessmentDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<AnalyticsResultDto> Handle(
        QueryAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var errors = new List<KeyValuePair<string, string>>();

        var request = AnalyticsRequestParser.Parse(query, today, errors)
            ?? throw new AppException(AnalyticsValues.ToErrorDictionary(errors));

        return await AnalyticsExecution.WithTimeoutAsync(async token =>
        {
            var groups = await ComputeAsync(request, today, token);
            var selection = AnalyticsResultBuilder.Select(request, groups);
            var labels = await LoadLabelsAsync(request, selection, token);

            return AnalyticsResultBuilder.ToDto(request, selection, labels, now);
        }, cancellationToken);
    }

    private Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        AnalyticsRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
        => request.Metric.Source switch
        {
            AnalyticsSource.Attempts => AttemptMetrics.ComputeAsync(_context, request, cancellationToken),
            AnalyticsSource.Cohorts => AttemptMetrics.ComputeCohortsAsync(_context, request, today, cancellationToken),
            AnalyticsSource.Answers => AnswerMetrics.ComputeAsync(_context, request, cancellationToken),
            AnalyticsSource.Questions => AnswerMetrics.ComputeQuestionsAsync(_context, request, cancellationToken),
            AnalyticsSource.PracticeSets => AnswerMetrics.ComputePracticeSetsAsync(_context, request, cancellationToken),
            AnalyticsSource.Exposure => AnswerMetrics.ComputeExposureAsync(_context, request, cancellationToken),
            AnalyticsSource.Bank => BankMetrics.ComputeAsync(_context, request, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown analytics source '{request.Metric.Source}'.")
        };

    /// <summary>Codes of the tests and practice sets in the rows, so callers can read them without another call.</summary>
    private async Task<IReadOnlyDictionary<string, string>?> LoadLabelsAsync(
        AnalyticsRequest request,
        AnalyticsSelection selection,
        CancellationToken cancellationToken)
    {
        var testIds = AnalyticsResultBuilder.IdsOf(request, selection, AnalyticsDimensions.Test);
        var practiceSetIds = AnalyticsResultBuilder.IdsOf(request, selection, AnalyticsDimensions.PracticeSet);
        var labels = new Dictionary<string, string>();

        if (testIds.Count > 0)
        {
            var tests = await _context.ToeicTests
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(test => testIds.Contains(test.Id))
                .Select(test => new { test.Id, test.Code })
                .ToListAsync(cancellationToken);

            foreach (var test in tests)
            {
                labels[test.Id.ToString("D")] = test.Code;
            }
        }

        if (practiceSetIds.Count > 0)
        {
            var practiceSets = await _context.ToeicPracticeSets
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(practiceSet => practiceSetIds.Contains(practiceSet.Id))
                .Select(practiceSet => new { practiceSet.Id, practiceSet.Code })
                .ToListAsync(cancellationToken);

            foreach (var practiceSet in practiceSets)
            {
                labels[practiceSet.Id.ToString("D")] = practiceSet.Code;
            }
        }

        return labels;
    }
}
