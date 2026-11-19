namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>
/// Short gloss of a transcript word: Vietnamese meaning, IPA, part of speech (lowercase English, "" when unknown),
/// example and its translation, English definition, the tier that gave the meaning and the official word when matched.
/// </summary>
public sealed record GlossEntryDto(string Meaning, string Ipa, string PartOfSpeech, string Example,
    string ExampleTranslation, string Definition, string Source, Guid? OfficialWordId)
{
    public static readonly GlossEntryDto Empty = new("", "", "", "", "", "", "none", null);
}
