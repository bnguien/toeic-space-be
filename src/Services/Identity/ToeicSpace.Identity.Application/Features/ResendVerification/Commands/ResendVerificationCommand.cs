using ToeicSpace.Identity.Application.Features.ResendVerification.Results;

namespace ToeicSpace.Identity.Application.Features.ResendVerification.Commands;

public sealed record ResendVerificationCommand(
    string Email) : IRequest<ResendVerificationResult>;
