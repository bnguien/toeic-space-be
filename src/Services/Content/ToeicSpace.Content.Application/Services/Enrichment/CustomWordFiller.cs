using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Services.Enrichment;

/// <summary>Writes enrichment results into the blank fields of a custom word (never overwrites, respects lengths).</summary>
public static class CustomWordFiller
{
    /// <summary>Returns the camelCase names of the fields that were filled.</summary>
    public static IReadOnlyList<string> FillBlanks(VocabularyWord word, WordGloss gloss, DateTime now)
    {
        var filled = new List<string>();
        void Set(GlossField field, string? current, string? value, int max, Action<string> assign)
        {
            if (!string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(value)) return;
            assign(Cut(value, max)!);
            filled.Add(field.Key());
        }
        Set(GlossField.MeaningVi, word.MeaningVi, gloss.MeaningVi, VocabularyWord.CustomMeaningMaxLength, v => word.MeaningVi = v);
        Set(GlossField.Phonetic, word.Phonetic, gloss.Phonetic, CustomWordInputValidator.PhoneticMaxLength, v => word.Phonetic = v);
        Set(GlossField.DefinitionEn, word.DefinitionEn, gloss.DefinitionEn, VocabularyWord.CustomTextMaxLength, v => word.DefinitionEn = v);
        Set(GlossField.Example, word.Example, gloss.Example, VocabularyWord.CustomTextMaxLength, v => word.Example = v);
        Set(GlossField.ExampleVi, word.ExampleVi, gloss.ExampleVi, VocabularyWord.CustomTextMaxLength, v => word.ExampleVi = v);
        if (filled.Count > 0) word.UpdatedAt = now;
        return filled;
    }

    public static string? Cut(string? value, int max)
    {
        if (value is null) return null;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max].TrimEnd();
    }
}
