using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.BuildingBlocks.Messaging;
using ToeicSpace.Identity.Infrastructure.Persistence;

namespace ToeicSpace.Identity.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcher(IServiceScopeFactory scopeFactory, IBus bus, OutboxOptions options, TimeProvider clock, ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollingIntervalSeconds), clock);
        do
        {
            try { await DispatchPendingAsync(stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogError(e, "Identity outbox dispatch cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task DispatchPendingAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var messages = await db.OutboxMessages.Where(x => x.ProcessedOn == null && x.AttemptCount < options.MaxAttempts)
            .OrderBy(x => x.OccurredOn).Take(options.BatchSize).ToListAsync(ct);
        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type, false);
                if (type is null || !typeof(IIntegrationEvent).IsAssignableFrom(type)) throw new InvalidOperationException($"Unknown integration event '{message.Type}'.");
                var value = JsonSerializer.Deserialize(message.Content, type, IdentityOutbox.SerializerOptions)
                    ?? throw new InvalidOperationException($"Outbox message {message.Id} is empty.");
                await bus.Publish(value, type, ct);
                message.ProcessedOn = clock.GetUtcNow().UtcDateTime;
                message.LastError = null;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                message.AttemptCount++;
                message.LastError = e.Message.Length > 2000 ? e.Message[..2000] : e.Message;
                logger.LogWarning(e, "Failed to publish Identity outbox message {MessageId}", message.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
