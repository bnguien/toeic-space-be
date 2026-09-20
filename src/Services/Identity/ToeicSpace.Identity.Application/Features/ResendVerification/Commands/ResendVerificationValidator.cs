namespace ToeicSpace.Identity.Application.Features.ResendVerification.Commands;

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
