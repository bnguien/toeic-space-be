namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>Word card details: synonyms, antonyms, senses per part of speech, word family and the basic gloss.</summary>
public sealed record WordInfoDto(string Word, string Ipa, string PartOfSpeech, IReadOnlyList<string> Synonyms,
    IReadOnlyList<string> Antonyms, IReadOnlyList<WordDefinitionDto> Definitions, IReadOnlyList<WordFamilyDto> Family,
    GlossEntryDto Base);
