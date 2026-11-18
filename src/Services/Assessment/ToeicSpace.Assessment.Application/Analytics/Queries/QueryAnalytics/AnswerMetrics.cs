using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using static ToeicSpace.Assessment.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Metrics over graded answers: overall, per question, per practice set and bank exposure.</summary>
public static class AnswerMetrics
{
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.AnswerPartials(context, request),
            cancellationToken);

        var metric = request.Metric.Name;

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions, Resolve, group =>
        {
            var answers = group.Sum(partial => partial.Answers);
            var learners = group.Select(partial => partial.UserId).Distinct().Count();

            return metric switch
            {
                AnswerCount => (answers, answers, learners),
                Accuracy => (AnalyticsGrouping.Ratio(group.Sum(partial => partial.Correct), answers), answers, learners),
                SkipRate => (AnalyticsGrouping.Ratio(group.Sum(partial => partial.Blank), answers), answers, learners),
                AvgTimePerQuestion => AverageTime(group, learners),
                _ => throw new InvalidOperationException($"Metric '{metric}' is not an answer metric.")
            };
        });
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeQuestionsAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.QuestionPartials(context, request),
            cancellationToken);

        var missed = request.Metric.Name == MostMissedQuestions;

        // The question is always a dimension, so every group holds exactly one question.
        return AnalyticsGrouping.Aggregate(partials, request.Dimensions, ResolveQuestion, group =>
        {
            var answers = group.Sum(partial => partial.Answers);
            var correct = group.Sum(partial => partial.Correct);
            var learners = group.Max(partial => partial.Learners);

            return missed
                ? (answers - correct, answers, learners)
                : (AnalyticsGrouping.Ratio(correct, answers), answers, learners);
        });
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputePracticeSetsAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.PracticeSetPartials(context, request),
            cancellationToken);

        var usage = request.Metric.Name == PracticeSetUsage;

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (_, partial) => partial.PracticeSetId,
            group =>
            {
                var partial = group.Single();
                return usage
                    ? (partial.Learners, partial.Learners, partial.Learners)
                    : (AnalyticsGrouping.Ratio(partial.Correct, partial.Answers), partial.Answers, partial.Learners);
            });
    }

    /// <summary>Share of active questions answered at least once in the range. Content metric: no suppression.</summary>
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeExposureAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var answered = (await AnalyticsExecution.LoadAsync(
                AnalyticsSources.AnsweredQuestionIds(context, request),
                cancellationToken))
            .ToHashSet();

        var questions = context.ToeicQuestions
            .AsNoTracking()
            .Where(question => question.Status == ContentStatus.Active);

        if (request.Part is { } part)
        {
            questions = questions.Where(question => question.Part == part);
        }

        if (request.Section is { } section)
        {
            questions = questions.Where(question => question.Section == section);
        }

        if (request.Difficulty is { } difficulty)
        {
            questions = questions.Where(question => question.DifficultyLevel == difficulty);
        }

        var active = await AnalyticsExecution.LoadAsync(
            questions.Select(question => new { question.Id, question.Part, question.DifficultyLevel }),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(active, request.Dimensions,
            (dimension, question) => dimension switch
            {
                AnalyticsDimensions.Part => question.Part,
                AnalyticsDimensions.Section => AnalyticsGrouping.SectionOf(question.Part),
                AnalyticsDimensions.Difficulty => question.DifficultyLevel,
                _ => null
            },
            group => (AnalyticsGrouping.Ratio(group.Count(question => answered.Contains(question.Id)), group.Count),
                group.Count,
                0));
    }

    private static (double? Value, int Count, int Learners) AverageTime(
        IReadOnlyCollection<AnswerPartial> group,
        int learners)
    {
        var timed = group.Sum(partial => partial.Timed);
        var seconds = group.Sum(partial => (long)partial.TimeSpentSeconds);

        return (timed == 0 ? null : (double)seconds / timed, timed, learners);
    }

    private static object? Resolve(string dimension, AnswerPartial partial) => dimension switch
    {
        AnalyticsDimensions.Test => partial.TestId,
        AnalyticsDimensions.Mode => partial.Mode,
        AnalyticsDimensions.Part => partial.Part,
        AnalyticsDimensions.Section => AnalyticsGrouping.SectionOf(partial.Part),
        AnalyticsDimensions.Difficulty => partial.Difficulty,
        _ => AnalyticsGrouping.TimeBucket(dimension, partial.Day)
    };

    private static object? ResolveQuestion(string dimension, QuestionPartial partial) => dimension switch
    {
        AnalyticsDimensions.Question => partial.QuestionId,
        AnalyticsDimensions.Part => partial.Part,
        AnalyticsDimensions.Section => AnalyticsGrouping.SectionOf(partial.Part),
        AnalyticsDimensions.Difficulty => partial.Difficulty,
        _ => null
    };
}
