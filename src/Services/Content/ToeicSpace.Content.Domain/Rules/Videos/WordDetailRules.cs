using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>Word card helpers (Studychill <c>word_detail.py</c>): accepted words, related-word filter, crude stem.</summary>
public static partial class WordDetailRules
{
    private static readonly string[] Suffixes =
        ["ation", "ition", "ment", "ness", "ity", "ive", "ing", "ion", "ed", "er", "ly", "al", "s"];

    [GeneratedRegex("^[a-z][a-z'-]{0,40}$")]
    private static partial Regex WordPattern();

    [GeneratedRegex("^[a-z][a-z-]{2,30}$")]
    private static partial Regex RelatedWordPattern();

    /// <summary>Lowercase single word of latin letters (apostrophe/hyphen allowed), else "".</summary>
    public static string NormalizeWord(string? word)
    {
        var value = (word ?? string.Empty).Trim().ToLowerInvariant();
        return WordPattern().IsMatch(value) ? value : string.Empty;
    }

    /// <summary>Adds a lowercase single-word synonym/antonym once, never the word itself (drops "ER", phrases).</summary>
    public static void AddUnique(List<string> items, string? candidate, string word)
    {
        var value = (candidate ?? string.Empty).Trim();
        if (RelatedWordPattern().IsMatch(value) && value != word && !items.Contains(value)) items.Add(value);
    }

    /// <summary>Strips the first matching suffix when at least 5 letters remain.</summary>
    public static string Stem(string word)
    {
        foreach (var suffix in Suffixes)
            if (word.EndsWith(suffix, StringComparison.Ordinal) && word.Length - suffix.Length >= 5)
                return word[..^suffix.Length];
        return word;
    }
}
