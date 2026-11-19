using ToeicSpace.Identity.Application.DTOs.Registration;


namespace ToeicSpace.Identity.Application.Features.Registration.Commands.ResendVerification;

public sealed record ResendVerificationCommand(
    string Email) : IRequest<ResendVerificationResult>;
