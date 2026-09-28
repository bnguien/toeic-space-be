namespace ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalSeconds { get; init; } = 5;

    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// After this many failed deliveries a message is left for manual inspection.
    /// </summary>
    public int MaxAttempts { get; init; } = 10;
}
