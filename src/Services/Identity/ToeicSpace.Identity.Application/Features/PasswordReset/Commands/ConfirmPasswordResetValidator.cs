using ToeicSpace.Identity.Application.Common;

namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed class ConfirmPasswordResetValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    public ConfirmPasswordResetValidator()
    {
        RuleFor(command => command.NewPassword).NewPassword();
        RuleFor(command => command.ConfirmPassword).NotEmpty().MaximumLength(128)
            .Equal(command => command.NewPassword).WithMessage("Passwords do not match.");
    }
}
