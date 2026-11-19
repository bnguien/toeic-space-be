namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>Enrichable fields of a word; <see cref="GlossFields.Key"/> gives the camelCase name used in sources maps.</summary>
public enum GlossField
{
    Phonetic, PartOfSpeech, MeaningVi, DefinitionEn, Example, ExampleVi, Cefr,
    PhraseType, Usage, Explanation, LiteralMeaning
}

public static class GlossFields
{
    public static string Key(this GlossField field) =>
        char.ToLowerInvariant(field.ToString()[0]) + field.ToString()[1..];

    public static GlossField? Parse(string key) =>
        Enum.TryParse<GlossField>(key, true, out var field) ? field : null;
}
