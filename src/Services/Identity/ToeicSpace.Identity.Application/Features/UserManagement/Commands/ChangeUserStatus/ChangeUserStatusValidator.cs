using ToeicSpace.Identity.Application.Features.UserManagement.Common;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserStatus;

public sealed class ChangeUserStatusValidator : AbstractValidator<ChangeUserStatusCommand>
{
    public ChangeUserStatusValidator()
    {
        RuleFor(command => command.Status)
            .Must(status => UserAdministrationRules.TryParseAssignableStatus(status, out _))
            .WithMessage("Status must be Active or Suspended.");
    }
}
