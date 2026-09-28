namespace ToeicSpace.BuildingBlocks.Messaging.Events;

/// <summary>
/// Asynchronous Service Contract: Emitted when a question's content or status changes in the Assessment question bank.
/// </summary>
public record QuestionUpdatedIntegrationEvent(
    Guid QuestionId,
    Guid? TestId,
    int Part,
    string Section,
    int DifficultyLevel,
    string Status,
    int Version,
    DateTime UpdatedAt
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
