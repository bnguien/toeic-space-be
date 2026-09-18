using MassTransit;

namespace ToeicSpace.BuildingBlocks.Messaging.MassTransit;

public class MassTransitEventPublisher : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return _publishEndpoint.Publish(integrationEvent, cancellationToken);
    }
}
