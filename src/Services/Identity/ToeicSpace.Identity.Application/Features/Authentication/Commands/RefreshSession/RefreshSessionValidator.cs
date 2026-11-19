namespace ToeicSpace.Identity.Application.Features.Authentication.Commands.RefreshSession;

public sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty()
            .MaximumLength(128);
    }
}
