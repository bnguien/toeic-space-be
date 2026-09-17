using ToeicSpace.Assessment.Application.Common.Content;

namespace ToeicSpace.Assessment.Application.Common.Validation;

public static class ValidationRuleExtensions
{
    public const int MaxUrlLength = 1000;

    public static IRuleBuilderOptions<T, string?> MustBeOptionalHttpUrl<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(MaxUrlLength)
            .Must(BeEmptyOrHttpUrl)
            .WithMessage("'{PropertyName}' must be an absolute http(s) URL.");
    }

    /// <summary>One URL, or several joined by <see cref="MediaUrls.ImageSeparator"/>.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeOptionalHttpUrls<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(MaxUrlLength)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || (!MediaUrls.HasEmptyItem(value) && MediaUrls.Split(value).All(BeEmptyOrHttpUrl)))
            .WithMessage($"'{{PropertyName}}' must be absolute http(s) URLs separated by '{MediaUrls.ImageSeparator}'.");
    }

    public static IRuleBuilderOptions<T, string> MustBeCode<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("'{PropertyName}' may only contain letters, digits, '.', '_' and '-'.");
    }

    private static bool BeEmptyOrHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        // Uri escapes '<', '>' and quotes silently; a stored URL must not carry markup.
        // Spaces are allowed: source file names contain them ("Test 09/98-100.mp3").
        return !value.Any(character => character is '<' or '>' or '"' || char.IsControl(character))
            && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
