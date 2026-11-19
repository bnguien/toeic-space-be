namespace ToeicSpace.Identity.Application.Features.Registration.Commands.ResendVerification;

public sealed class ResendVerificationValidator
    : AbstractValidator<ResendVerificationCommand>
{
    public ResendVerificationValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();
    }
}
