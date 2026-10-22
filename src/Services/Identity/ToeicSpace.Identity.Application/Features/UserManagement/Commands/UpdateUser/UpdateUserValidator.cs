using ToeicSpace.Identity.Application.Features.Register.Commands;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.UpdateUser;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Phone)
            .Must(IdentityNormalizer.IsValidPhone)
            .WithMessage("Phone must be a valid Vietnamese mobile phone number.");
    }
}
