using System.Text.RegularExpressions;

namespace ToeicSpace.Assessment.Application.Common.Validation;

/// <summary>
/// Content fields may contain simple HTML (paragraphs, lists, tables). Clients render it through
/// an allow-list, and the API additionally refuses markup that could run code in a browser.
/// </summary>
public static partial class RichTextRules
{
    public static IRuleBuilderOptions<T, string?> MustBeSafeRichText<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(BeSafe)
            .WithMessage("'{PropertyName}' contains markup that is not allowed (scripts, embedded content or event handlers).");
    }

    public static bool BeSafe(string? value)
        => string.IsNullOrEmpty(value) || !UnsafeMarkup().IsMatch(value);

    [GeneratedRegex(
        @"<\s*/?\s*(script|style|iframe|frame|frameset|object|embed|applet|form|input|button|textarea|select|link|meta|base|svg|math|template|noscript)\b" +
        @"|<[^>]*\son[a-z]+\s*=" +
        @"|<[^>]*\b(href|src|action|formaction|xlink:href)\s*=\s*[""']?\s*(javascript|vbscript|data)\s*:",
        RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeMarkup();
}
