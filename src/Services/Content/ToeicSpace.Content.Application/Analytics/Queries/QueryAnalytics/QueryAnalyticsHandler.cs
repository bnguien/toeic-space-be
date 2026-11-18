using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.DTOs.Analytics;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;

public sealed class QueryAnalyticsHandler : IRequestHandler<QueryAnalyticsQuery, AnalyticsResultDto>
{
    private readonly IContentAnalyticsRepository _repository;
    private readonly TimeProvider _timeProvider;

    public QueryAnalyticsHandler(
        IContentAnalyticsRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository;
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
            ?? throw AnalyticsErrors.InvalidQuery(AnalyticsValues.ToErrorDictionary(errors));

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
            AnalyticsSource.Courses or AnalyticsSource.Lessons => CourseMetrics.ComputeAsync(_repository, request, cancellationToken),
            AnalyticsSource.StudySessions => StudyMetrics.ComputeSessionsAsync(_repository, request, cancellationToken),
            AnalyticsSource.ActiveLearners => StudyMetrics.ComputeActiveLearnersAsync(_repository, request, cancellationToken),
            AnalyticsSource.Reviews => VocabularyMetrics.ComputeReviewsAsync(_repository, request, cancellationToken),
            AnalyticsSource.MasteredWords => VocabularyMetrics.ComputeWordsLearnedAsync(_repository, request, cancellationToken),
            AnalyticsSource.SavedWords => VocabularyMetrics.ComputeSavedWordsAsync(_repository, request, cancellationToken),
            AnalyticsSource.Dictation => DictationMetrics.ComputeAsync(_repository, request, cancellationToken),
            AnalyticsSource.GrammarAttempts => GrammarMetrics.ComputeAttemptsAsync(_repository, request, cancellationToken),
            AnalyticsSource.GrammarAnswers => GrammarMetrics.ComputeAccuracyAsync(_repository, request, cancellationToken),
            AnalyticsSource.GrammarLessons => GrammarMetrics.ComputeLessonCompletionAsync(_repository, request, cancellationToken),
            AnalyticsSource.VideoWatches => MediaMetrics.ComputeVideoAsync(_repository, request, cancellationToken),
            AnalyticsSource.NewsReadings => MediaMetrics.ComputeNewsAsync(_repository, request, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown analytics source '{request.Metric.Source}'.")
        };

    /// <summary>
    /// Codes / titles of the courses, stages, videos and articles in the rows, so callers can read them without
    /// another call. Grammar document values are already readable codes.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> LoadLabelsAsync(
        AnalyticsRequest request,
        AnalyticsSelection selection,
        CancellationToken cancellationToken)
    {
        var labels = new Dictionary<string, string>();

        void Add<TKey>(IReadOnlyDictionary<TKey, string> found) where TKey : notnull
        {
            foreach (var (key, label) in found)
            {
                labels[AnalyticsGrouping.Format(string.Empty, key)!] = label;
            }
        }

        var courseIds = AnalyticsResultBuilder.ValuesOf<Guid>(request, selection, AnalyticsDimensions.Course);
        if (courseIds.Count > 0)
        {
            Add(await _repository.GetCourseCodesAsync(courseIds, cancellationToken));
        }

        var dictationCourseIds = AnalyticsResultBuilder.ValuesOf<int>(request, selection, AnalyticsDimensions.Course);
        if (dictationCourseIds.Count > 0)
        {
            Add(await _repository.GetDictationCourseCodesAsync(dictationCourseIds, cancellationToken));
        }

        var stageIds = AnalyticsResultBuilder.ValuesOf<Guid>(request, selection, AnalyticsDimensions.Stage);
        if (stageIds.Count > 0)
        {
            Add(await _repository.GetStageSlugsAsync(stageIds, cancellationToken));
        }

        var videoIds = AnalyticsResultBuilder.ValuesOf<string>(request, selection, AnalyticsDimensions.Video);
        if (videoIds.Count > 0)
        {
            Add(await _repository.GetVideoTitlesAsync(videoIds, cancellationToken));
        }

        var articleIds = AnalyticsResultBuilder.ValuesOf<Guid>(request, selection, AnalyticsDimensions.Article);
        if (articleIds.Count > 0)
        {
            Add(await _repository.GetArticleSlugsAsync(articleIds, cancellationToken));
        }

        return labels;
    }
}
