namespace ToeicSpace.Identity.Application.Messaging.Events;

public sealed record UserRegistrationOtpRequestedEvent(
    string Email,
    string FullName,
    string Otp);
