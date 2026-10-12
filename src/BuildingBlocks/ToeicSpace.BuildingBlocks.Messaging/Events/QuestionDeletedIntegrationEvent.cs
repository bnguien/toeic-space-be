namespace ToeicSpace.BuildingBlocks.Messaging.Events;

/// <summary>
/// Asynchronous Service Contract: Emitted when a question is soft deleted from the Assessment question bank.
/// </summary>
public record QuestionDeletedIntegrationEvent(
    Guid QuestionId,
    Guid? TestId,
    DateTime DeletedAt
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
