using ToeicSpace.Identity.Application.Services;


namespace ToeicSpace.Identity.Application.Features.Users.Commands.ChangeUserStatus;

public sealed class ChangeUserStatusValidator : AbstractValidator<ChangeUserStatusCommand>
{
    public ChangeUserStatusValidator()
    {
        RuleFor(command => command.Status)
            .Must(status => UserAdministrationRules.TryParseAssignableStatus(status, out _))
            .WithMessage("Status must be Active or Suspended.");
    }
}
