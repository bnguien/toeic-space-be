namespace ToeicSpace.Identity.Application.Features.Passwords.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254).EmailAddress();
    }
}
