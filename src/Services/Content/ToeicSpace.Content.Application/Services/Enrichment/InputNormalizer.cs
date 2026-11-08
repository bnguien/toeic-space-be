using System.Text.RegularExpressions;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Services.Enrichment;

/// <summary>Normalised user input: display text, lookup key and kind.</summary>
public sealed record NormalizedInput(string Text, string Key, WordKind Kind, int TokenCount);

/// <summary>Trim, collapse whitespace, cap at 100 characters; word = 1 token, phrase = 2–5, else reject.</summary>
public static partial class InputNormalizer
{
    public const int MaxLength = 100;
    public const int MaxPhraseTokens = 5;

    public static NormalizedInput Normalize(string? text)
    {
        var collapsed = string.Join(' ', (text ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length > MaxLength) collapsed = collapsed[..MaxLength].TrimEnd();
        var tokens = collapsed.Length == 0 ? 0 : collapsed.Split(' ').Length;
        var kind = tokens == 0 || tokens > MaxPhraseTokens || !HasLatinLetter(collapsed) ? WordKind.Reject
            : tokens == 1 ? WordKind.Word
            : WordKind.Phrase;
        return new NormalizedInput(collapsed, VocabularyWord.Normalize(collapsed), kind, tokens);
    }

    public static bool HasLatinLetter(string? text) => text is not null && LatinLetter().IsMatch(text);

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinLetter();
}
