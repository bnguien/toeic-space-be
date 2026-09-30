namespace ToeicSpace.Identity.Application.Common;

public static class PasswordValidation
{
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilderInitial<T, string> rule)
        => rule.Cascade(CascadeMode.Stop)
            .NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter (A-Z).")
            .Matches(@"[^\p{L}\p{N}\s]").WithMessage("Password must contain at least one special character.");
}
