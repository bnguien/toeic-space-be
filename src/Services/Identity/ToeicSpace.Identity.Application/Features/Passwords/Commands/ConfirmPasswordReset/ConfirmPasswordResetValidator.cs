using ToeicSpace.Identity.Application.Common.Validation;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.Passwords.Commands.ConfirmPasswordReset;

public sealed class ConfirmPasswordResetValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    public ConfirmPasswordResetValidator()
    {
        RuleFor(command => command.Token).Cascade(CascadeMode.Stop)
            .Must(token => !string.IsNullOrEmpty(token))
            .WithMessage("The password reset token is required.")
            .WithErrorCode(ErrorCodes.TokenInvalid)
            .MaximumLength(128)
            .WithMessage("The password reset token must not exceed 128 characters.")
            .WithErrorCode(ErrorCodes.TokenInvalid);
        RuleFor(command => command.NewPassword).NewPassword();
        RuleFor(command => command.ConfirmPassword).NotEmpty().MaximumLength(128)
            .Equal(command => command.NewPassword).WithMessage("Passwords do not match.");
    }
}
