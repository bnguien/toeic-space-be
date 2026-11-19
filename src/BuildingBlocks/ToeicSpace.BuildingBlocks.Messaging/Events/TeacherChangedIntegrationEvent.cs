namespace ToeicSpace.BuildingBlocks.Messaging.Events;

/// <summary>Identity-owned snapshot used by downstream teacher projections.</summary>
public sealed record TeacherChangedIntegrationEvent(
    Guid TeacherId,
    string Name,
    string Email,
    string? AvatarUrl,
    string Role,
    string Status,
    bool IsEligible,
    long Version) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Requests a full Identity snapshot for initial sync and projection repair.</summary>
public sealed record TeacherSynchronizationRequestedIntegrationEvent(Guid RequestId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
