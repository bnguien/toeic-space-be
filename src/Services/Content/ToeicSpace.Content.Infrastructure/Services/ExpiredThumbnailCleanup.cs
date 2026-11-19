using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;

namespace ToeicSpace.Content.Infrastructure.Services;

public sealed class ExpiredThumbnailCleanup(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ExpiredThumbnailCleanup> logger) : BackgroundService
{
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        // Wait a day after URL expiry so an in-flight confirmation cannot race cleanup.
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        IReadOnlyList<string> keys;
        await using (var scope = scopes.CreateAsyncScope())
            keys = (await scope.ServiceProvider.GetRequiredService<IThumbnailUploadRepository>()
                .GetExpiredPendingAsync(cutoff, 100, ct)).Select(x => x.Key).ToArray();
        foreach (var key in keys)
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                var uploads = scope.ServiceProvider.GetRequiredService<IThumbnailUploadRepository>();
                var upload = await uploads.GetAsync(key, ct);
                if (upload is null || upload.ConfirmedAt is not null || upload.ExpiresAt >= cutoff) continue;
                if (await uploads.IsReferencedByCourseAsync(key, ct))
                {
                    logger.LogWarning("Skipped cleanup of course thumbnail {Key} because a course references it", key);
                    continue;
                }
                await scope.ServiceProvider.GetRequiredService<IThumbnailObjectStore>().DeleteAsync(key, ct);
                uploads.Remove(upload);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                logger.LogInformation("Removed abandoned course thumbnail {Key}", key);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogWarning(ex, "Failed to clean up abandoned course thumbnail {Key}", key); }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogWarning(ex, "Course thumbnail cleanup cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
