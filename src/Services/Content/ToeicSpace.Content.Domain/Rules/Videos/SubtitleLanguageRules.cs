using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>Language choices for a video's subtitle list (Studychill <c>subtitles.py</c> / <c>videoLang.js</c>).</summary>
public static class SubtitleLanguageRules
{
    private static readonly string[] PermanentErrors =
        ["not available", "unavailable", "private video", "has been removed", "been terminated"];

    /// <summary>Every code (manual first, then auto), without duplicates.</summary>
    public static IReadOnlyList<string> LanguageCodes(IEnumerable<VideoLanguage> manual, IEnumerable<VideoLanguage> auto)
    {
        var codes = new List<string>();
        foreach (var language in manual.Concat(auto))
            if (!string.IsNullOrEmpty(language.Code) && !codes.Contains(language.Code)) codes.Add(language.Code);
        return codes;
    }

    /// <summary>
    /// Maps the reported original language to a code that really exists in the list ('-orig' = original ASR first);
    /// unknown language → uploaded English, en-orig, first upload, any -orig, en, first code.
    /// </summary>
    public static string PickOriginalLanguage(string? language, IReadOnlyList<VideoLanguage> manual,
        IReadOnlyList<VideoLanguage> auto)
    {
        var codes = LanguageCodes(manual, auto);
        if (codes.Count == 0) return string.Empty;
        var lower = new Dictionary<string, string>();
        foreach (var code in codes) lower.TryAdd(code.ToLowerInvariant(), code);
        var lang = (language ?? string.Empty).Trim().ToLowerInvariant();
        var root = lang.Length > 0 ? lang.Split('-')[0] : string.Empty;
        string First(Func<string, bool> predicate) => codes.FirstOrDefault(c => predicate(c.ToLowerInvariant())) ?? string.Empty;

        if (lang.Length > 0)
        {
            foreach (var candidate in new[] { lang + "-orig", lang, root + "-orig", root })
                if (lower.TryGetValue(candidate, out var hit)) return hit;
            var prefixed = First(c => c.StartsWith(root + "-", StringComparison.Ordinal) && c.EndsWith("-orig", StringComparison.Ordinal));
            if (prefixed.Length == 0) prefixed = First(c => c.StartsWith(root + "-", StringComparison.Ordinal));
            if (prefixed.Length > 0) return prefixed;
        }

        var uploaded = manual.Select(x => x.Code).Where(c => !string.IsNullOrEmpty(c)).ToList();
        var english = uploaded.FirstOrDefault(c => VideoLinks.IsEnglish(c));
        if (!string.IsNullOrEmpty(english)) return english;
        if (lower.TryGetValue("en-orig", out var orig)) return orig;
        if (uploaded.Count > 0) return uploaded[0];
        var anyOrig = First(c => c.EndsWith("-orig", StringComparison.Ordinal));
        if (anyOrig.Length > 0) return anyOrig;
        return lower.TryGetValue("en", out var en) ? en : codes[0];
    }

    /// <summary>
    /// Real English subtitles: uploaded English, or the original ASR of an English video. An auto 'en' of a video in
    /// another language is a machine translation and does not count.
    /// </summary>
    public static bool HasEnglish(IReadOnlyList<VideoLanguage> manual, IReadOnlyList<VideoLanguage> auto, string? language)
    {
        if (manual.Any(x => VideoLinks.IsEnglish(x.Code ?? string.Empty))) return true;
        var autoCodes = auto.Select(x => (x.Code ?? string.Empty).ToLowerInvariant()).ToHashSet();
        var lang = (language ?? string.Empty).ToLowerInvariant();
        return autoCodes.Contains("en-orig")
            || (lang.Split('-')[0] == "en" && (autoCodes.Contains("en") || autoCodes.Contains(lang)));
    }

    /// <summary>The video cannot be watched any more (removed, private, region-blocked) — unlike 429/network errors.</summary>
    public static bool IsPermanentError(string? error)
    {
        var lowered = (error ?? string.Empty).ToLowerInvariant();
        return !lowered.Contains("429") && PermanentErrors.Any(lowered.Contains);
    }
}
