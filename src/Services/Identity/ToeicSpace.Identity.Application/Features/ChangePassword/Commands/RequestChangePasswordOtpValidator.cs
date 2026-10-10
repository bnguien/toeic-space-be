namespace ToeicSpace.Identity.Application.Features.ChangePassword.Commands;

public sealed class RequestChangePasswordOtpValidator : AbstractValidator<RequestChangePasswordOtpCommand>
{
    public RequestChangePasswordOtpValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(128);
    }
}
