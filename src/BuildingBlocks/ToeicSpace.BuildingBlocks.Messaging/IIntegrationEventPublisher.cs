namespace ToeicSpace.BuildingBlocks.Messaging;

/// <summary>
/// Service contract for publishing asynchronous integration events onto the Message Broker.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : class;
}
