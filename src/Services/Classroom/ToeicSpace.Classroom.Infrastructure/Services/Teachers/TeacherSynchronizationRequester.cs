using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToeicSpace.BuildingBlocks.Messaging;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Infrastructure.Services.Teachers;

public sealed class TeacherSynchronizationRequester(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<TeacherSynchronizationRequester> logger)
    : BackgroundService, ITeacherSynchronizationRequester
{
    public async Task RequestAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        await publisher.PublishAsync(new TeacherSynchronizationRequestedIntegrationEvent(Guid.NewGuid()), ct);
        logger.LogDebug("Requested teacher projection synchronization");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), clock);
        do
        {
            try
            {
                await RequestAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not request Teacher projection synchronization");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
