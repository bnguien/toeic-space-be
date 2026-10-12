using System.Text.RegularExpressions;

namespace ToeicSpace.Assessment.Application.Common.Content;

/// <summary>
/// Imported content stores the Vietnamese translation in the same column as the original:
/// passages after a <c>&lt;translation_split&gt;</c> marker, Part 3/4 transcripts after an <c>&lt;hr&gt;</c>.
/// The API returns the two halves separately so translations can be withheld from learners
/// (a Part 6 translation spells out the missing sentences).
/// </summary>
public static partial class TranslatedText
{
    public static string? PassageText(string? content)
        => Split(content, PassageSeparator()).Text;

    public static string? PassageTranslation(string? content)
        => Split(content, PassageSeparator()).Translation;

    public static string? TranscriptText(string? transcript)
        => Split(transcript, TranscriptSeparator()).Text;

    public static string? TranscriptTranslation(string? transcript)
        => Split(transcript, TranscriptSeparator()).Translation;

    private static (string? Text, string? Translation) Split(string? value, Regex separator)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var parts = separator.Split(value, count: 2);

        return parts.Length == 1
            ? (value.Trim(), null)
            : (NullIfBlank(parts[0]), NullIfBlank(LeadingBreaks().Replace(parts[1], string.Empty)));
    }

    private static string? NullIfBlank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"<\s*translation_split\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex PassageSeparator();

    [GeneratedRegex(@"<\s*hr\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex TranscriptSeparator();

    [GeneratedRegex(@"^(\s|<\s*br\s*/?\s*>)+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingBreaks();
}
