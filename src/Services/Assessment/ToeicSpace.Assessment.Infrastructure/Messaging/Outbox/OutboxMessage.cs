namespace ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;

/// <summary>
/// Integration event stored in the same transaction as the business change and
/// delivered to the message broker by <see cref="OutboxDispatcher"/>.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// Assembly-qualified CLR type name of the integration event.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime OccurredOn { get; set; }

    public DateTime? ProcessedOn { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
