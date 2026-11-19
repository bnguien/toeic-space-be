using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;

namespace ToeicSpace.Content.Infrastructure.Services;

public sealed class ExpiredLessonUploadCleanup(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ExpiredLessonUploadCleanup> logger) : BackgroundService
{
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        IReadOnlyList<string> keys;
        await using (var scope = scopes.CreateAsyncScope())
            keys = (await scope.ServiceProvider.GetRequiredService<ILessonUploadRepository>()
                .GetCleanupCandidatesAsync(cutoff, 100, ct)).Select(x => x.Key).ToArray();
        foreach (var key in keys)
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                var uploads = scope.ServiceProvider.GetRequiredService<ILessonUploadRepository>();
                var upload = await uploads.GetOwnedAsync(key, ExtractOwner(key), ct);
                if (upload is null || upload.CreatedAt > cutoff ||
                    upload.ConfirmedAt is { } confirmed && confirmed >= cutoff || upload.ExpiresAt >= cutoff)
                    continue;
                if (await scope.ServiceProvider.GetRequiredService<ILessonRepository>()
                    .IsObjectKeyUsedAsync(key, ct)) continue;
                await scope.ServiceProvider.GetRequiredService<ILessonMediaStore>().DeleteAsync(key, ct);
                uploads.Remove(upload);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                logger.LogInformation("Removed unused lesson media {ObjectKey}", key);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogWarning(ex, "Failed cleaning lesson media {ObjectKey}", key); }
        }
    }

    private static Guid ExtractOwner(string key)
    {
        var segments = key.Split('/');
        return segments.Length >= 4 && Guid.TryParseExact(segments[2], "N", out var owner)
            ? owner : Guid.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogWarning(ex, "Lesson media cleanup cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
