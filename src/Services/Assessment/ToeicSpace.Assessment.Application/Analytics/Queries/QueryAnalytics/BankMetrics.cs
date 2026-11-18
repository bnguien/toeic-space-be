using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Domain.Rules;
using static ToeicSpace.Assessment.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Counts over the current content (soft-deleted items excluded). No learner data is involved.</summary>
public static class BankMetrics
{
    public static Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
        => request.Metric.Name switch
        {
            QuestionCount => CountQuestionsAsync(context, request, cancellationToken),
            PassageCount => CountPassagesAsync(context, request, cancellationToken),
            TestCount => CountTestsAsync(context, request, cancellationToken),
            PracticeSetCount => CountPracticeSetsAsync(context, request, cancellationToken),
            PracticeSetSize => MeasurePracticeSetSizesAsync(context, request, cancellationToken),
            TestCompleteness => MeasureTestCompletenessAsync(context, request, cancellationToken),
            _ => throw new InvalidOperationException($"Metric '{request.Metric.Name}' is not a bank metric.")
        };

    private static async Task<IReadOnlyList<AnalyticsGroup>> CountQuestionsAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var query = context.ToeicQuestions.AsNoTracking();

        if (request.Part is { } part)
        {
            query = query.Where(question => question.Part == part);
        }

        if (request.Section is { } section)
        {
            query = query.Where(question => question.Section == section);
        }

        if (request.Difficulty is { } difficulty)
        {
            query = query.Where(question => question.DifficultyLevel == difficulty);
        }

        if (request.ContentStatus is { } status)
        {
            query = query.Where(question => question.Status == status);
        }

        if (request.TestId is { } testId)
        {
            query = query.Where(question => question.TestId == testId);
        }

        var counts = await AnalyticsExecution.LoadAsync(
            query
                .GroupBy(question => new { question.Part, question.DifficultyLevel, question.Status, question.TestId })
                .Select(group => new
                {
                    group.Key.Part,
                    group.Key.DifficultyLevel,
                    group.Key.Status,
                    group.Key.TestId,
                    Questions = group.Count()
                }),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(counts, request.Dimensions,
            (dimension, row) => dimension switch
            {
                AnalyticsDimensions.Part => row.Part,
                AnalyticsDimensions.Section => AnalyticsGrouping.SectionOf(row.Part),
                AnalyticsDimensions.Difficulty => row.DifficultyLevel,
                AnalyticsDimensions.Status => row.Status,
                AnalyticsDimensions.Test => row.TestId,
                _ => null
            },
            group => Count(group.Sum(row => row.Questions)));
    }

    private static async Task<IReadOnlyList<AnalyticsGroup>> CountPassagesAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var query = context.ToeicPassages.AsNoTracking();

        if (request.Part is { } part)
        {
            query = query.Where(passage => passage.Part == part);
        }

        if (request.Section is { } section)
        {
            var parts = PartsOf(section);
            query = query.Where(passage => parts.Contains(passage.Part));
        }

        if (request.TestId is { } testId)
        {
            query = query.Where(passage => passage.TestId == testId);
        }

        var counts = await AnalyticsExecution.LoadAsync(
            query
                .GroupBy(passage => new { passage.Part, passage.TestId })
                .Select(group => new { group.Key.Part, group.Key.TestId, Passages = group.Count() }),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(counts, request.Dimensions,
            (dimension, row) => dimension switch
            {
                AnalyticsDimensions.Part => row.Part,
                AnalyticsDimensions.Section => AnalyticsGrouping.SectionOf(row.Part),
                AnalyticsDimensions.Test => row.TestId,
                _ => null
            },
            group => Count(group.Sum(row => row.Passages)));
    }

    private static async Task<IReadOnlyList<AnalyticsGroup>> CountTestsAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var query = context.ToeicTests.AsNoTracking();

        if (request.ContentStatus is { } status)
        {
            query = query.Where(test => test.Status == status);
        }

        var counts = await AnalyticsExecution.LoadAsync(
            query
                .GroupBy(test => test.Status)
                .Select(group => new { Status = group.Key, Tests = group.Count() }),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(counts, request.Dimensions,
            (_, row) => row.Status,
            group => Count(group.Sum(row => row.Tests)));
    }

    private static async Task<IReadOnlyList<AnalyticsGroup>> CountPracticeSetsAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var counts = await AnalyticsExecution.LoadAsync(
            FilteredPracticeSets(context, request)
                .GroupBy(practiceSet => new { practiceSet.Part, practiceSet.Kind, practiceSet.Status })
                .Select(group => new { group.Key.Part, group.Key.Kind, group.Key.Status, Sets = group.Count() }),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(counts, request.Dimensions,
            (dimension, row) => dimension switch
            {
                AnalyticsDimensions.Part => row.Part,
                AnalyticsDimensions.Section => AnalyticsGrouping.SectionOf(row.Part),
                AnalyticsDimensions.Kind => row.Kind,
                AnalyticsDimensions.Status => row.Status,
                _ => null
            },
            group => Count(group.Sum(row => row.Sets)));
    }

    private static async Task<IReadOnlyList<AnalyticsGroup>> MeasurePracticeSetSizesAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var sizes = await AnalyticsExecution.LoadAsync(
            FilteredPracticeSets(context, request)
                .Select(practiceSet => new { practiceSet.Id, Questions = practiceSet.Items.Count() }),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(sizes, request.Dimensions,
            (_, row) => row.Id,
            group => Count(group.Sum(row => row.Questions)));
    }

    private static async Task<IReadOnlyList<AnalyticsGroup>> MeasureTestCompletenessAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var query = context.ToeicTests.AsNoTracking();

        if (request.ContentStatus is { } status)
        {
            query = query.Where(test => test.Status == status);
        }

        var tests = await AnalyticsExecution.LoadAsync(
            query.Select(test => new { test.Id, test.TotalQuestions, Questions = test.Questions.Count() }),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(tests, request.Dimensions,
            (_, row) => row.Id,
            group =>
            {
                var test = group.Single();
                return (AnalyticsGrouping.Ratio(test.Questions, test.TotalQuestions), test.Questions, 0);
            });
    }

    private static IQueryable<ToeicPracticeSet> FilteredPracticeSets(
        IAssessmentDbContext context,
        AnalyticsRequest request)
    {
        var query = context.ToeicPracticeSets.AsNoTracking();

        if (request.Part is { } part)
        {
            query = query.Where(practiceSet => practiceSet.Part == part);
        }

        if (request.Section is { } section)
        {
            var parts = PartsOf(section);
            query = query.Where(practiceSet => parts.Contains(practiceSet.Part));
        }

        if (request.ContentStatus is { } status)
        {
            query = query.Where(practiceSet => practiceSet.Status == status);
        }

        return query;
    }

    private static List<ToeicPart> PartsOf(ToeicSection section)
        => Enum.GetValues<ToeicPart>()
            .Where(part => ToeicPartRules.GetSection(part) == section)
            .ToList();

    private static (double? Value, int Count, int Learners) Count(int total)
        => (total, total, 0);
}
