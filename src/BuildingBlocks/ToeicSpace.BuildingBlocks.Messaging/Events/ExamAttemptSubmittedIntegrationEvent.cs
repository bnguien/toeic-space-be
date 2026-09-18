namespace ToeicSpace.BuildingBlocks.Messaging.Events;

/// <summary>
/// Asynchronous Service Contract: Emitted when an exam attempt is submitted and scored.
/// Consumed by Classroom Service (Leaderboard/Assignments) and Learning Service (Analytics/Recommendations).
/// </summary>
public record ExamAttemptSubmittedIntegrationEvent(
    Guid AttemptId,
    Guid UserId,
    Guid TestId,
    string TestCode,
    string TestTitle,
    string Category,
    int ScoreListening,
    int ScoreReading,
    int TotalScore,
    int CorrectAnswersCount,
    int TotalQuestions,
    int DurationSpentSeconds,
    DateTime CompletedAt
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
