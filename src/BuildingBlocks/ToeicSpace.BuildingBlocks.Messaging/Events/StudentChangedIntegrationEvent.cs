namespace ToeicSpace.BuildingBlocks.Messaging.Events;

/// <summary>Identity-owned student snapshot for downstream enrollment eligibility.</summary>
public sealed record StudentChangedIntegrationEvent(
    Guid StudentId, string Name, string Email, string Role, string Status,
    bool IsVerified, bool IsEligible, long Version) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public sealed record StudentSynchronizationRequestedIntegrationEvent(Guid RequestId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
