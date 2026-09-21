using System.Text.RegularExpressions;

namespace ToeicSpace.Assessment.Application.Common.Content;

/// <summary>
/// Image columns of multi-page Part 7 passages hold several URLs joined by
/// <c>&lt;image_split&gt;</c> (imported from Studychill). Clients split the value the same way.
/// </summary>
public static partial class MediaUrls
{
    public const string ImageSeparator = "<image_split>";

    public static IReadOnlyList<string> Split(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : Separator().Split(value)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();

    public static bool HasEmptyItem(string value)
        => Separator().Split(value).Any(string.IsNullOrWhiteSpace);

    [GeneratedRegex(@"<\s*image_split\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex Separator();
}
