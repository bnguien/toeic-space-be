namespace ToeicSpace.BuildingBlocks.Messaging.Events;

/// <summary>
/// Asynchronous Service Contract: Emitted when a question is newly created in the Assessment question bank.
/// </summary>
public record QuestionCreatedIntegrationEvent(
    Guid QuestionId,
    Guid? TestId,
    int Part,
    string Section,
    int DifficultyLevel,
    DateTime CreatedAt
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
