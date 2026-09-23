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
            .MaximumLength(128)
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("Password must contain at least one uppercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain at least one digit.")
            .Must(password => password.Any(character =>
                !char.IsLetterOrDigit(character) &&
                !char.IsWhiteSpace(character)))
            .WithMessage("Password must contain at least one special character.");

        RuleFor(command => command.ConfirmPassword)
            .NotEmpty()
            .MaximumLength(128);
    }
}
