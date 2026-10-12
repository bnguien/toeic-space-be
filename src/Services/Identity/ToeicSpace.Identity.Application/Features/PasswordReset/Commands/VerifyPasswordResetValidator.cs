namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed class VerifyPasswordResetValidator : AbstractValidator<VerifyPasswordResetCommand>
{
    public VerifyPasswordResetValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(command => command.Otp).NotEmpty().Matches("^[0-9]{6}$");
    }
}
