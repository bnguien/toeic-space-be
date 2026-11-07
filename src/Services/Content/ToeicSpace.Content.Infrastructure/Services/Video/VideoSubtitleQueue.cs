using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Application.Services.Video;

namespace ToeicSpace.Content.Infrastructure.Services.Video;

/// <summary>A queued background job: prepare tracks of (video, languages) or fix the Vietnamese track of a video.</summary>
public sealed record VideoSubtitleJob(string VideoId, IReadOnlyList<string> Langs, string? FixSourceLang);

/// <summary>
/// Single-flight queue of subtitle preparation (Studychill Celery task <c>prepare_video_subs</c> / <c>fix_video_vi</c>):
/// one job per (video, sorted languages) while it is queued or running; the outcome is remembered (ok 6 h,
/// failure 60 s) so the next poll returns what exists instead of queueing again.
/// </summary>
public sealed class VideoSubtitleQueue(IVideoCache cache) : IVideoSubtitlePreparer
{
    private readonly Channel<VideoSubtitleJob> _channel = Channel.CreateBounded<VideoSubtitleJob>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite });

    public ChannelReader<VideoSubtitleJob> Reader => _channel.Reader;

    public static string LangsKey(IEnumerable<string> langs) =>
        string.Join(',', langs.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));

    public static string LockKey(string videoId, IEnumerable<string> langs) => $"video:subs-lock:{videoId}:{LangsKey(langs)}";
    public static string OutcomeKey(string videoId, IEnumerable<string> langs) => $"video:subs-done:{videoId}:{LangsKey(langs)}";
    public static string FixKey(string videoId) => $"video:vi-fix-lock:{videoId}";

    public bool RequestPrepare(string videoId, IReadOnlyList<string> langs)
    {
        var key = LockKey(videoId, langs);
        if (!cache.TryAdd(key, true, IVideoSubtitlePreparer.LockTtl)) return false;
        if (_channel.Writer.TryWrite(new VideoSubtitleJob(videoId, langs.ToList(), null))) return true;
        cache.Remove(key);
        return false;
    }

    public SubtitlePrepareOutcome? GetOutcome(string videoId, IReadOnlyList<string> langs) =>
        cache.TryGet<SubtitlePrepareOutcome>(OutcomeKey(videoId, langs), out var outcome) ? outcome : null;

    public bool RequestViFix(string videoId, string sourceLang)
    {
        if (cache.TryGet<bool>(SubtitlePreparationService.ViCheckedKey(videoId), out _)
            || cache.TryGet<bool>(SubtitlePreparationService.ViFailedKey(videoId), out _)) return false;
        var key = FixKey(videoId);
        if (!cache.TryAdd(key, true, IVideoSubtitlePreparer.LockTtl)) return false;
        if (_channel.Writer.TryWrite(new VideoSubtitleJob(videoId, [], sourceLang))) return true;
        cache.Remove(key);
        return false;
    }

    internal void Complete(VideoSubtitleJob job, bool ok)
    {
        if (job.FixSourceLang is not null)
        {
            cache.Remove(FixKey(job.VideoId));
            return;
        }
        // Outcome first, then release the lock: a poll in between must not queue a second job.
        cache.Set(OutcomeKey(job.VideoId, job.Langs), new SubtitlePrepareOutcome(ok),
            ok ? IVideoSubtitlePreparer.OkOutcomeTtl : IVideoSubtitlePreparer.FailedOutcomeTtl);
        cache.Remove(LockKey(job.VideoId, job.Langs));
    }
}

/// <summary>Runs queued subtitle jobs two at a time, each in its own scope with a 240 s limit.</summary>
public sealed class VideoSubtitleWorker(VideoSubtitleQueue queue, IServiceScopeFactory scopes,
    ILogger<VideoSubtitleWorker> logger) : BackgroundService
{
    public const int Parallelism = 2;
    public static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(240);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, Parallelism).Select(_ => RunAsync(stoppingToken)));

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            var ok = false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(JobTimeout);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<SubtitlePreparationService>();
                if (job.FixSourceLang is { } source) await service.FixStoredVietnameseAsync(job.VideoId, source, timeout.Token);
                else ok = await service.PrepareAsync(job.VideoId, job.Langs, timeout.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Subtitle job {VideoId} [{Langs}] failed", job.VideoId, string.Join(',', job.Langs));
            }
            finally
            {
                queue.Complete(job, ok);
            }
        }
    }
}
