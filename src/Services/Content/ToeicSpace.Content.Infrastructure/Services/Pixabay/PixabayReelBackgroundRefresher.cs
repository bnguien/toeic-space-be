using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Services.Pixabay;

/// <summary>
/// Refreshes the Reel background cache from Pixabay at startup and then every 24 hours (results are cached for at
/// least 24 hours, as Pixabay requires). Clips are upserted by (pixabay, id); a clip missing from
/// <see cref="ReelBackground.MaxMisses"/> consecutive refreshes is deactivated.
/// </summary>
public sealed class PixabayReelBackgroundRefresher(IServiceScopeFactory scopes,
    IOptions<PixabayOptions> options, TimeProvider clock, ILogger<PixabayReelBackgroundRefresher> logger)
    : BackgroundService
{
    private static readonly HashSet<string> PeopleTags =
    [
        "people", "person", "persons", "woman", "women", "man", "men", "girl", "girls", "boy", "boys",
        "child", "children", "kid", "kids", "baby", "family", "couple"
    ];

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey)) return 0;

        await using var scope = scopes.CreateAsyncScope();
        // Resolved per run so the typed client's handler is rotated by IHttpClientFactory.
        var client = scope.ServiceProvider.GetRequiredService<PixabayVideoClient>();
        var clips = new Dictionary<string, PixabayVideoHit>();
        var failedQueries = 0;
        for (var i = 0; i < settings.Queries.Length; i++)
        {
            if (i > 0 && settings.RequestDelay > TimeSpan.Zero) await Task.Delay(settings.RequestDelay, clock, ct);
            try
            {
                foreach (var hit in await client.SearchAsync(settings.BaseUrl, settings.ApiKey, settings.Queries[i],
                             settings.PerPage, ct))
                    if (IsUsable(hit, settings.MinDurationSeconds)) clips.TryAdd(hit.Id.ToString(), hit);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                           or System.Text.Json.JsonException && !ct.IsCancellationRequested)
            {
                failedQueries++;
                logger.LogWarning("Pixabay query {Query} failed: {Error}", settings.Queries[i], ex.Message);
            }
        }
        if (clips.Count == 0)
        {
            // Never count misses on a failed refresh, or an outage would deactivate the whole cache.
            logger.LogWarning("Pixabay refresh returned no usable clips ({FailedQueries} failed queries)", failedQueries);
            return 0;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var repository = scope.ServiceProvider.GetRequiredService<IReelBackgroundRepository>();
        var existing = (await repository.GetByProviderForUpdateAsync(ReelBackground.PixabayProvider, ct))
            .ToDictionary(x => x.ProviderId);
        var created = 0;
        foreach (var (id, hit) in clips)
        {
            if (!existing.Remove(id, out var row))
            {
                row = new ReelBackground { Provider = ReelBackground.PixabayProvider, ProviderId = id, CreatedAt = now };
                repository.Add(row);
                created++;
            }
            Apply(row, hit, now);
        }
        // Only a complete refresh may count misses, so a single failed query cannot age out its clips.
        if (failedQueries == 0)
            foreach (var missing in existing.Values) missing.MarkMissed(now);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        logger.LogInformation("Refreshed {Count} Pixabay Reel backgrounds ({Created} new, {Missing} missing)",
            clips.Count, created, failedQueries == 0 ? existing.Count : 0);
        return clips.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning("Pixabay:ApiKey is not configured; Reel backgrounds will not be refreshed");
            return;
        }
        using var timer = new PeriodicTimer(settings.RefreshInterval, clock);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogWarning(ex, "Pixabay Reel background refresh failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Picks the small rendition, then medium, then tiny.</summary>
    internal static PixabayVideoFile? PickRendition(PixabayVideoRenditions? videos) =>
        new[] { videos?.Small, videos?.Medium, videos?.Tiny }
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x?.Url) && !string.IsNullOrWhiteSpace(x.Thumbnail));

    internal static bool IsUsable(PixabayVideoHit hit, int minDurationSeconds) =>
        hit.Id > 0 && hit.Duration >= minDurationSeconds && !string.IsNullOrWhiteSpace(hit.PageUrl)
        && PickRendition(hit.Videos) is not null && !ShowsPeople(hit.Tags);

    internal static bool ShowsPeople(string? tags) =>
        (tags ?? string.Empty).Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(tag => PeopleTags.Contains(tag.ToLowerInvariant()));

    private static void Apply(ReelBackground row, PixabayVideoHit hit, DateTime now)
    {
        var file = PickRendition(hit.Videos)!;
        row.VideoUrl = Truncate(file.Url!, 1000);
        row.PosterUrl = Truncate(file.Thumbnail!, 1000);
        row.PageUrl = Truncate(hit.PageUrl!, 1000);
        row.Author = Truncate(hit.User ?? string.Empty, 200);
        row.Tags = Truncate(hit.Tags ?? string.Empty, 500);
        row.DurationSeconds = hit.Duration;
        row.Width = file.Width;
        row.Height = file.Height;
        row.LastSeenAt = now;
        row.MissCount = 0;
        row.IsActive = true;
        row.UpdatedAt = now;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
