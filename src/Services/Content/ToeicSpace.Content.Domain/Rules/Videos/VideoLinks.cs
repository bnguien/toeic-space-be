using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>YouTube link parsing and the id/language-code checks that also guard every store key.</summary>
public static partial class VideoLinks
{
    public const int MaxVideoIdLength = 32;
    public const int MaxLangLength = 12;

    /// <summary>Video id from any YouTube link form (watch?v=, youtu.be/, /embed/, /shorts/, /live/, /v/) or a bare id.</summary>
    public static string ExtractVideoId(string? url)
    {
        var raw = (url ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;
        // A bare id pasted from somewhere else.
        if (raw.Length <= 20 && !raw.Contains('/') && !raw.Contains('.') && !raw.Contains('?')) return raw;

        var split = raw.Split('?', 2);
        var path = split[0];
        var query = split.Length > 1 ? split[1] : string.Empty;
        foreach (var part in query.Split('&'))
            if (part.StartsWith("v=", StringComparison.Ordinal)) return part[2..].Split('#')[0];

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return string.Empty;
        foreach (var marker in new[] { "embed", "shorts", "live", "v" })
        {
            var i = Array.IndexOf(segments, marker);
            if (i >= 0 && i + 1 < segments.Length) return segments[i + 1].Split('#')[0];
        }
        return path.Contains("youtu.be", StringComparison.Ordinal) ? segments[^1].Split('#')[0] : string.Empty;
    }

    /// <summary>Letters, digits, '-' and '_' only (≤ 32): also the path-traversal guard for store keys.</summary>
    public static bool IsValidVideoId(string? videoId) => videoId is { Length: > 0 and <= MaxVideoIdLength }
        && videoId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>Exactly the 11-character id the IFrame player reports.</summary>
    public static bool IsEmbedVideoId(string? videoId) => videoId is not null && EmbedId().IsMatch(videoId);

    /// <summary>Subtitle language code: letters, digits and '-' (vi, en-orig, zh-Hans, pt-BR), ≤ 12.</summary>
    public static bool IsValidLang(string? code) => code is { Length: > 0 and <= MaxLangLength }
        && code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public static string ThumbnailUrl(string videoId) => $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";

    public static string BaseLanguage(string code) => code.ToLowerInvariant().Split('-')[0];

    public static bool IsEnglish(string code) => BaseLanguage(code) == "en";

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex EmbedId();
}
