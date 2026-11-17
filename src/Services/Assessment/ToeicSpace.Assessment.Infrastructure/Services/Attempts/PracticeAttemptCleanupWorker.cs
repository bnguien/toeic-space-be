using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Attempts;

namespace ToeicSpace.Assessment.Infrastructure.Services.Attempts;

public sealed class PracticeAttemptCleanupWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<PracticeAttemptCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60), clock);
        do
        {
            var watch = Stopwatch.StartNew();
            try
            {
                int deleted;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    deleted = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CleanupPracticeAttempts(), stoppingToken);
                } while (deleted == 100 && !stoppingToken.IsCancellationRequested);
                logger.LogInformation("Practice cleanup cycle succeeded in {ElapsedMs}ms", watch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogError("Practice cleanup cycle failed ({ErrorType}); will retry next cycle", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
