using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Infrastructure.Persistence;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;

/// <summary>
/// Periodically publishes pending outbox messages to the message broker (at-least-once delivery).
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBus _bus;
    private readonly OutboxOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IBus bus,
        OutboxOptions options,
        TimeProvider timeProvider,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _bus = bus;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), _timeProvider);

        do
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Outbox dispatch cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AssessmentDbContext>();

        var messages = await context.OutboxMessages
            .Where(message => message.ProcessedOn == null && message.AttemptCount < _options.MaxAttempts)
            .OrderBy(message => message.OccurredOn)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var eventType = ResolveEventType(message.Type);
                var integrationEvent = JsonSerializer.Deserialize(message.Content, eventType, OutboxIntegrationEventPublisher.SerializerOptions)
                    ?? throw new InvalidOperationException($"Outbox message {message.Id} has empty content.");

                await _bus.Publish(integrationEvent, eventType, cancellationToken);

                message.ProcessedOn = _timeProvider.GetUtcNow().UtcDateTime;
                message.LastError = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.AttemptCount++;
                message.LastError = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;

                _logger.LogWarning(
                    exception,
                    "Failed to publish outbox message {MessageId} of type {MessageType} (attempt {AttemptCount})",
                    message.Id,
                    message.Type,
                    message.AttemptCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Type ResolveEventType(string typeName)
    {
        var type = Type.GetType(typeName, throwOnError: false);

        // Only integration event contracts may be instantiated from stored type names.
        if (type is null || !typeof(IIntegrationEvent).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"'{typeName}' is not a known integration event type.");
        }

        return type;
    }
}
