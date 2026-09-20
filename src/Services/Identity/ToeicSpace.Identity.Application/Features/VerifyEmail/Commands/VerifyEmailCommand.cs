using ToeicSpace.Identity.Application.Features.VerifyEmail.Results;

namespace ToeicSpace.Identity.Application.Features.VerifyEmail.Commands;

public sealed record VerifyEmailCommand(
    string ChallengeId,
    string Otp) : IRequest<VerifyEmailResult>;
