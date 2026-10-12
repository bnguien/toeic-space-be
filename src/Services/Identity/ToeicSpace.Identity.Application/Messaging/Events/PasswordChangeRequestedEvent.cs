namespace ToeicSpace.Identity.Application.Messaging.Events;

// The recipient is resolved from the authenticated user's id; no credentials enter the broker.
public sealed record PasswordChangeRequestedEvent(Guid UserId, string RequestId);
