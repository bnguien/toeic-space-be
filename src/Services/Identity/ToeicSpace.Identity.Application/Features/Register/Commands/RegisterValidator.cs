namespace ToeicSpace.Identity.Application.Features.Register.Commands;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
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
            .WithMessage("Phone must be a valid Vietnamese or international phone number.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(command => command.ConfirmPassword)
            .NotEmpty()
            .MaximumLength(128);
    }
}
