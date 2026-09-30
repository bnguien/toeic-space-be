namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254).EmailAddress();
    }
}
