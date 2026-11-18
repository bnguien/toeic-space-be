namespace ToeicSpace.Assessment.Domain.Entities;

// Payloads are private persisted data, never serialized as API entities.
public sealed class ToeicAttemptSnapshot
{
    public Guid AttemptId { get; set; }
    public ToeicAttempt Attempt { get; set; } = null!;
    public int SchemaVersion { get; set; } = 1;
    public DateTime CapturedAt { get; set; }
    public string Payload { get; set; } = "";
}

public sealed class ToeicAttemptQuestion
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid SourceQuestionId { get; set; }
    public int Version { get; set; }
    public int OrderIndex { get; set; }
    public string Payload { get; set; } = "";
}

// A lifecycle fence, not progress/history. It survives permanent deletion of an Attempt.
public sealed class PracticeAttemptState
{
    public Guid UserId { get; set; }
    public Guid PracticeSetId { get; set; }
    public long Generation { get; set; }
}
