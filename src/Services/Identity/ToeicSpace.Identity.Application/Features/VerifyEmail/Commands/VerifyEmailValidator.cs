namespace ToeicSpace.Identity.Application.Features.VerifyEmail.Commands;

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(command => command.ChallengeId)
            .NotEmpty()
            .Must(challengeId => Guid.TryParseExact(challengeId, "N", out _))
            .WithMessage("ChallengeId is invalid.");

        RuleFor(command => command.Otp)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("Otp must contain exactly 6 digits.");
    }
}
