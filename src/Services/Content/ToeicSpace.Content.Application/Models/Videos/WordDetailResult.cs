namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>Synonyms, antonyms, one sense per part of speech (≤ 3) and the US IPA of a word from the dictionary.</summary>
public sealed record WordDetailResult(IReadOnlyList<string> Synonyms, IReadOnlyList<string> Antonyms,
    IReadOnlyList<WordSense> Senses, string Ipa, string PartOfSpeech)
{
    public static readonly WordDetailResult NotFound = new([], [], [], string.Empty, string.Empty);
}

public sealed record WordSense(string PartOfSpeech, string Definition, string Example);
