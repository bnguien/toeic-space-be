namespace ToeicSpace.BuildingBlocks.Messaging;

/// <summary>
/// Base contract marker interface for all asynchronous integration events in TOEIC Space SOA.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredOn { get; }
}
