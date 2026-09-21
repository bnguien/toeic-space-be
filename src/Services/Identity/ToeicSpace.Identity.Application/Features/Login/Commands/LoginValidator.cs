namespace ToeicSpace.Identity.Application.Features.Login.Commands;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        // No complexity rules here: they would hint at the password policy.
        // The upper bound stops oversized inputs from reaching the key derivation.
        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}
