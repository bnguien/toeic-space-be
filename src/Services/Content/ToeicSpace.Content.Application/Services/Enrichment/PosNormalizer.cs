using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Services.Enrichment;

/// <summary>
/// Maps any part-of-speech label (abbreviations, dictionary/AI wording, Vietnamese names, "(n)", "[adj]") to
/// <see cref="PartOfSpeech"/>: phrasal verbs, idioms and other multi-word labels become Phrase; unknown labels Other.
/// </summary>
public static class PosNormalizer
{
    private static readonly Dictionary<string, PartOfSpeech> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["n"] = PartOfSpeech.Noun, ["noun"] = PartOfSpeech.Noun, ["nouns"] = PartOfSpeech.Noun,
        ["danh từ"] = PartOfSpeech.Noun, ["dt"] = PartOfSpeech.Noun,
        ["v"] = PartOfSpeech.Verb, ["vt"] = PartOfSpeech.Verb, ["vi"] = PartOfSpeech.Verb, ["verb"] = PartOfSpeech.Verb,
        ["verbs"] = PartOfSpeech.Verb, ["động từ"] = PartOfSpeech.Verb, ["đgt"] = PartOfSpeech.Verb,
        ["adj"] = PartOfSpeech.Adjective, ["a"] = PartOfSpeech.Adjective, ["adjective"] = PartOfSpeech.Adjective,
        ["tính từ"] = PartOfSpeech.Adjective, ["tt"] = PartOfSpeech.Adjective,
        ["adv"] = PartOfSpeech.Adverb, ["adverb"] = PartOfSpeech.Adverb, ["trạng từ"] = PartOfSpeech.Adverb,
        ["phó từ"] = PartOfSpeech.Adverb,
        ["prep"] = PartOfSpeech.Preposition, ["preposition"] = PartOfSpeech.Preposition,
        ["giới từ"] = PartOfSpeech.Preposition,
        ["conj"] = PartOfSpeech.Conjunction, ["conjunction"] = PartOfSpeech.Conjunction,
        ["liên từ"] = PartOfSpeech.Conjunction,
        ["pron"] = PartOfSpeech.Pronoun, ["pronoun"] = PartOfSpeech.Pronoun, ["đại từ"] = PartOfSpeech.Pronoun,
        ["det"] = PartOfSpeech.Determiner, ["determiner"] = PartOfSpeech.Determiner,
        ["article"] = PartOfSpeech.Determiner, ["mạo từ"] = PartOfSpeech.Determiner,
        ["interj"] = PartOfSpeech.Interjection, ["interjection"] = PartOfSpeech.Interjection,
        ["exclamation"] = PartOfSpeech.Interjection, ["thán từ"] = PartOfSpeech.Interjection,
        ["phr"] = PartOfSpeech.Phrase, ["phrase"] = PartOfSpeech.Phrase, ["idiom"] = PartOfSpeech.Phrase,
        ["phrasal verb"] = PartOfSpeech.Phrase, ["phrasal_verb"] = PartOfSpeech.Phrase,
        ["collocation"] = PartOfSpeech.Phrase, ["cụm từ"] = PartOfSpeech.Phrase, ["thành ngữ"] = PartOfSpeech.Phrase,
        ["other"] = PartOfSpeech.Other, ["num"] = PartOfSpeech.Other, ["number"] = PartOfSpeech.Other,
        ["numeral"] = PartOfSpeech.Other
    };

    /// <summary>Null for a blank label; otherwise always a value (Other when unknown).</summary>
    public static PartOfSpeech? Normalize(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var text = string.Join(' ', label.Trim().Trim('(', ')', '[', ']', '{', '}', '.', ',', ';', ':', ' ')
            .Replace('.', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0) return null;
        if (Map.TryGetValue(text, out var pos)) return pos;
        return text.Contains(' ') ? PartOfSpeech.Phrase : PartOfSpeech.Other;
    }
}
