using ToeicSpace.Identity.Application.DTOs.Registration;


namespace ToeicSpace.Identity.Application.Features.Registration.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(
    string ChallengeId,
    string Otp) : IRequest<VerifyEmailResult>;
