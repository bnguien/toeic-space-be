using ToeicSpace.Content.Application.DTOs.VocabularyLookup;
using ToeicSpace.Content.Application.Models.Enrichment;

namespace ToeicSpace.Content.Application.Mappings;

public static class EnrichmentMappingExtensions
{
    public static WordLookupDto ToLookupDto(this EnrichResult x, ExistingWordDto? existing)
    {
        var g = x.Gloss;
        return new WordLookupDto(g.Text, g.Kind.Key(), g.Headword, g.Phonetic, g.PartOfSpeech, g.MeaningVi,
            g.DefinitionEn, g.Example, g.ExampleVi, g.Cefr, g.PhraseType, g.Usage, g.Explanation, g.LiteralMeaning,
            g.Sources.ToDictionary(s => s.Key.Key(), s => s.Value.Key()), x.Complete, x.AiUsed, existing,
            x.OfficialWordId);
    }
}
