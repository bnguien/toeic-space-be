namespace ToeicSpace.Identity.Application.Interfaces.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken)
        where TEvent : class;
}
