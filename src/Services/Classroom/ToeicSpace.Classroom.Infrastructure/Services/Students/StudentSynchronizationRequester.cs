using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.BuildingBlocks.Messaging;
using ToeicSpace.BuildingBlocks.Messaging.Events;

namespace ToeicSpace.Classroom.Infrastructure.Services.Students;

public sealed class StudentSynchronizationRequester(IServiceScopeFactory scopes, TimeProvider clock, ILogger<StudentSynchronizationRequester> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                await publisher.PublishAsync(new StudentSynchronizationRequestedIntegrationEvent(Guid.NewGuid()), stoppingToken);
                logger.LogDebug("Requested student projection synchronization");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not request Student projection synchronization");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
