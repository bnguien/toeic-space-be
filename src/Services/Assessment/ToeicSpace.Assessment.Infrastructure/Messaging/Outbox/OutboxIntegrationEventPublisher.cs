using System.Text.Json;
using ToeicSpace.Assessment.Infrastructure.Persistence;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;

/// <summary>
/// Adds the event to the outbox of the current DbContext. Nothing is sent until the caller's
/// SaveChangesAsync commits, so an event is published if and only if the business change is saved.
/// </summary>
public sealed class OutboxIntegrationEventPublisher : IIntegrationEventPublisher
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AssessmentDbContext _context;
    private readonly TimeProvider _timeProvider;

    public OutboxIntegrationEventPublisher(
        AssessmentDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var eventType = integrationEvent.GetType();

        _context.OutboxMessages.Add(new OutboxMessage
        {
            Id = integrationEvent is IIntegrationEvent withId ? withId.Id : Guid.NewGuid(),
            Type = eventType.AssemblyQualifiedName ?? eventType.FullName!,
            Content = JsonSerializer.Serialize(integrationEvent, eventType, SerializerOptions),
            OccurredOn = _timeProvider.GetUtcNow().UtcDateTime
        });

        return Task.CompletedTask;
    }
}
