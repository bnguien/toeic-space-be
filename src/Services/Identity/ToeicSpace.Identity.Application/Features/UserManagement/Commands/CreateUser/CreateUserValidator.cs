using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.CreateUser;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(command => command.Phone)
            .Must(IdentityNormalizer.IsValidPhone)
            .WithMessage("Phone must be a valid Vietnamese mobile phone number.");

        RuleFor(command => command.Role)
            .Must(role => UserAdministrationRules.TryParseRole(role, out _))
            .WithMessage("Role must be User, Teacher or Admin.");
    }
}
