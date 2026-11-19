using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.CleanupAttempts;
using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Infrastructure.Services.Attempts;

// Every minute: auto-submits timed tests past their deadline, then deletes expired practice attempts.
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
                AttemptCleanupResult result;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CleanupAttemptsCommand(), stoppingToken);
                } while (result.HasMore && !stoppingToken.IsCancellationRequested);
                logger.LogInformation("Practice cleanup cycle succeeded in {ElapsedMs}ms", watch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogError("Practice cleanup cycle failed ({ErrorType}); will retry next cycle", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
