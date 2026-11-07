using System.Text;
using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Application.Features.GrammarComments;

/// <summary>Comment targets and text normalisation of the grammar comments.</summary>
public static partial class GrammarCommentRules
{
    public const int PageSize = 20;
    public const int RepliesPreview = 3;
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(2);

    /// <summary>Discussion of a topic's document (Kiến thức tab): <c>topic-04:document</c>.</summary>
    [GeneratedRegex(@"^topic-(0[1-9]|[1-3]\d|4[0-4]):document$")]
    public static partial Regex Target();

    [GeneratedRegex(@"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]")]
    private static partial Regex Control();

    /// <summary>NFC, CRLF → LF, control characters (except LF/TAB) removed, trimmed.</summary>
    public static string Normalize(string? text)
    {
        var value = (text ?? string.Empty).Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n');
        return Control().Replace(value, string.Empty).Trim();
    }
}
