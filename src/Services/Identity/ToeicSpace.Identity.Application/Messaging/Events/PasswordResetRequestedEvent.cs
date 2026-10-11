namespace ToeicSpace.Identity.Application.Messaging.Events;

// No OTP or reset credential is placed on the message broker.
public sealed record PasswordResetRequestedEvent(string Email, string RequestId);
