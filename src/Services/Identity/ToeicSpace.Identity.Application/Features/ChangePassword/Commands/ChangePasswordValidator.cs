using ToeicSpace.Identity.Application.Common;

namespace ToeicSpace.Identity.Application.Features.ChangePassword.Commands;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Otp).NotEmpty().Matches("^[0-9]{6}$");
        RuleFor(command => command.NewPassword).NewPassword();
        RuleFor(command => command.ConfirmPassword).NotEmpty().MaximumLength(128)
            .Equal(command => command.NewPassword).WithMessage("Passwords do not match.");
    }
}
