using Hangfire;
using Microsoft.Extensions.Hosting;

namespace ToeicSpace.Identity.Infrastructure.Jobs;

internal sealed class HangfireRecurringJobRegistrar : IHostedService
{
    public const string InactiveAccountCleanupJobId =
        "identity-inactive-account-cleanup";

    private readonly IRecurringJobManager _recurringJobManager;

    public HangfireRecurringJobRegistrar(
        IRecurringJobManager recurringJobManager)
    {
        _recurringJobManager = recurringJobManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _recurringJobManager.AddOrUpdate<InactiveAccountCleanupJob>(
            InactiveAccountCleanupJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Hourly);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
