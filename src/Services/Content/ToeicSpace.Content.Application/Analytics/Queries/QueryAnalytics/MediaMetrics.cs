using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using static ToeicSpace.Content.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Metrics over bilingual videos and bilingual news readings.</summary>
public static class MediaMetrics
{
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeVideoAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Metric.Name;
        var moment = name == VideoWatchTime ? VideoWatchMoment.LastProgress : VideoWatchMoment.FirstOpened;
        var watches = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetVideoWatchesAsync(request.Range!, moment, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(watches, request.Dimensions,
            (dimension, watch) => dimension == AnalyticsDimensions.Video
                ? watch.VideoId
                : AnalyticsGrouping.TimeBucket(dimension, watch.At),
            group =>
            {
                var learners = AnalyticsGrouping.Learners(group, watch => watch.LearnerId);

                return name switch
                {
                    VideoWatches => (group.Count, group.Count, learners),
                    VideoWatchTime => (group.Sum(WatchedSeconds), learners, learners),
                    _ => throw new InvalidOperationException($"Metric '{name}' is not a video metric.")
                };
            });
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeNewsAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var readings = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetNewsReadingsAsync(request.Range!, take, token),
            cancellationToken);

        var name = request.Metric.Name;

        return AnalyticsGrouping.Aggregate(readings, request.Dimensions,
            (dimension, reading) => dimension == AnalyticsDimensions.Article
                ? reading.ArticleId
                : AnalyticsGrouping.TimeBucket(dimension, reading.OpenedAt),
            group =>
            {
                var learners = AnalyticsGrouping.Learners(group, reading => reading.LearnerId);

                return name switch
                {
                    NewsReads => (group.Count, group.Count, learners),
                    NewsReadCompletion => (AnalyticsGrouping.Ratio(group.Count(reading => reading.Completed), group.Count),
                        group.Count, learners),
                    _ => throw new InvalidOperationException($"Metric '{name}' is not a news metric.")
                };
            });
    }

    /// <summary>Seconds up to the furthest point watched: furthest percent of the video duration.</summary>
    public static double WatchedSeconds(VideoWatchFact watch)
        => Math.Max(0, watch.Duration) * Math.Clamp(watch.WatchedPercent, 0, 100) / 100.0;
}
