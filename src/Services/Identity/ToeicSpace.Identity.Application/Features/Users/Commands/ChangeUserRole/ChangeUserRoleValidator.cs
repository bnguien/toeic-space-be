using ToeicSpace.Identity.Application.Services;


namespace ToeicSpace.Identity.Application.Features.Users.Commands.ChangeUserRole;

public sealed class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleValidator()
    {
        RuleFor(command => command.Role)
            .Must(role => UserAdministrationRules.TryParseRole(role, out _))
            .WithMessage("Role must be User, Teacher or Admin.");
    }
}
